﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fasterflect;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Alpha;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using QuantConnect.Securities.Option;
using QuantConnect.Securities.Volatility;
using QuantConnect.Util.RateLimit;

namespace QuantConnect.Lean.Engine
{
    /// <summary>
    /// Algorithm manager class executes the algorithm and generates and passes through the algorithm events.
    /// </summary>
    public class AlgorithmManager
    {
        private IAlgorithm _algorithm;
        private readonly object _lock;
        private readonly bool _liveMode;

        /// <summary>
        /// Publicly accessible algorithm status
        /// </summary>
        public AlgorithmStatus State => _algorithm?.Status ?? AlgorithmStatus.Running;

        /// <summary>
        /// Public access to the currently running algorithm id.
        /// </summary>
        public string AlgorithmId { get; private set; }

        /// <summary>
        /// Provides the isolator with a function for verifying that we're not spending too much time in each
        /// algorithm manager time loop
        /// </summary>
        public AlgorithmTimeLimitManager TimeLimit { get; }

        /// <summary>
        /// Quit state flag for the running algorithm. When true the user has requested the backtest stops through a Quit() method.
        /// </summary>
        /// <seealso cref="QCAlgorithm.Quit(String)"/>
        public bool QuitState => State == AlgorithmStatus.Deleted;

        /// <summary>
        /// Gets the number of data points processed per second
        /// </summary>
        public long DataPoints { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmManager"/> class
        /// </summary>
        /// <param name="liveMode">True if we're running in live mode, false for backtest mode</param>
        /// <param name="job">Provided by LEAN when creating a new algo manager. This is the job
        /// that the algo manager is about to execute. Research and other consumers can provide the
        /// default value of null</param>
        public AlgorithmManager(bool liveMode, AlgorithmNodePacket job = null)
        {
            AlgorithmId = "";
            _liveMode = liveMode;
            _lock = new object();

            // initialize the time limit manager
            TimeLimit = new AlgorithmTimeLimitManager(
                CreateTokenBucket(job?.Controls?.TrainingLimits),
                TimeSpan.FromMinutes(Config.GetDouble("algorithm-manager-time-loop-maximum", 20))
            );
        }

        /// <summary>
        /// Launch the algorithm manager to run this strategy
        /// </summary>
        /// <param name="job">Algorithm job</param>
        /// <param name="algorithm">Algorithm instance</param>
        /// <param name="synchronizer">Instance which implements <see cref="ISynchronizer"/>. Used to stream the data</param>
        /// <param name="transactions">Transaction manager object</param>
        /// <param name="results">Result handler object</param>
        /// <param name="realtime">Realtime processing object</param>
        /// <param name="leanManager">ILeanManager implementation that is updated periodically with the IAlgorithm instance</param>
        /// <param name="alphas">Alpha handler used to process algorithm generated insights</param>
        /// <param name="token">Cancellation token</param>
        /// <remarks>Modify with caution</remarks>
        public void Run(AlgorithmNodePacket job, IAlgorithm algorithm, ISynchronizer synchronizer, ITransactionHandler transactions, IResultHandler results, IRealTimeHandler realtime, ILeanManager leanManager, IAlphaHandler alphas, CancellationToken token)
        {
            //Initialize:
            DataPoints = 0;
            _algorithm = algorithm;

            var backtestMode = (job.Type == PacketType.BacktestNode);
            var methodInvokers = new Dictionary<Type, MethodInvoker>();
            var marginCallFrequency = TimeSpan.FromMinutes(5);
            var nextMarginCallTime = DateTime.MinValue;
            var settlementScanFrequency = TimeSpan.FromMinutes(30);
            var nextSettlementScanTime = DateTime.MinValue;
            var time = algorithm.StartDate.Date;

            var delistings = new List<Delisting>();
            var splitWarnings = new List<Split>();

            //Initialize Properties:
            AlgorithmId = job.AlgorithmId;
            _algorithm.Status = AlgorithmStatus.Running;

            //Create the method accessors to push generic types into algorithm: Find all OnData events:

            // Algorithm 2.0 data accessors
            var hasOnDataTradeBars = AddMethodInvoker<TradeBars>(algorithm, methodInvokers);
            var hasOnDataQuoteBars = AddMethodInvoker<QuoteBars>(algorithm, methodInvokers);
            var hasOnDataOptionChains = AddMethodInvoker<OptionChains>(algorithm, methodInvokers);
            var hasOnDataTicks = AddMethodInvoker<Ticks>(algorithm, methodInvokers);

            // dividend and split events
            var hasOnDataDividends = AddMethodInvoker<Dividends>(algorithm, methodInvokers);
            var hasOnDataSplits = AddMethodInvoker<Splits>(algorithm, methodInvokers);
            var hasOnDataDelistings = AddMethodInvoker<Delistings>(algorithm, methodInvokers);
            var hasOnDataSymbolChangedEvents = AddMethodInvoker<SymbolChangedEvents>(algorithm, methodInvokers);

            //Go through the subscription types and create invokers to trigger the event handlers for each custom type:
            foreach (var config in algorithm.SubscriptionManager.Subscriptions)
            {
                //If type is a custom feed, check for a dedicated event handler
                if (config.IsCustomData)
                {
                    //Get the matching method for this event handler - e.g. public void OnData(Quandl data) { .. }
                    var genericMethod = (algorithm.GetType()).GetMethod("OnData", new[] { config.Type });

                    //If we already have this Type-handler then don't add it to invokers again.
                    if (methodInvokers.ContainsKey(config.Type)) continue;

                    if (genericMethod != null)
                    {
                        methodInvokers.Add(config.Type, genericMethod.DelegateForCallMethod());
                    }
                }
            }

            //Loop over the queues: get a data collection, then pass them all into relevent methods in the algorithm.
            Log.Trace("AlgorithmManager.Run(): Begin DataStream - Start: " + algorithm.StartDate + " Stop: " + algorithm.EndDate);
            foreach (var timeSlice in Stream(algorithm, synchronizer, results, token))
            {
                // reset our timer on each loop
                TimeLimit.StartNewTimeStep();

                //Check this backtest is still running:
                if (_algorithm.Status != AlgorithmStatus.Running)
                {
                    Log.Error($"AlgorithmManager.Run(): Algorithm state changed to {_algorithm.Status} at {timeSlice.Time.ToStringInvariant()}");
                    break;
                }

                //Execute with TimeLimit Monitor:
                if (token.IsCancellationRequested)
                {
                    Log.Error($"AlgorithmManager.Run(): CancellationRequestion at {timeSlice.Time.ToStringInvariant()}");
                    return;
                }

                // Update the ILeanManager
                leanManager.Update();

                time = timeSlice.Time;
                DataPoints += timeSlice.DataPointCount;

                // We need to sample at the top of the loop in case we have a strategy
                // with no data added. Time pulses would be emitted between days, and
                // would cause us to skip sampling of the portfolio in those dead days.
                results.Sample(time);

                if (backtestMode)
                {
                    if (algorithm.Portfolio.TotalPortfolioValue <= 0)
                    {
                        var logMessage = "AlgorithmManager.Run(): Portfolio value is less than or equal to zero, stopping algorithm.";
                        Log.Error(logMessage);
                        results.SystemDebugMessage(logMessage);
                        break;
                    }

                    // If backtesting, we need to check if there are realtime events in the past
                    // which didn't fire because at the scheduled times there was no data (i.e. markets closed)
                    // and fire them with the correct date/time.
                    realtime.ScanPastEvents(time);
                }

                //Set the algorithm and real time handler's time
                algorithm.SetDateTime(time);

                // the time pulse are just to advance algorithm time, lets shortcut the loop here
                if (timeSlice.IsTimePulse)
                {
                    continue;
                }

                // Update the current slice before firing scheduled events or any other task
                algorithm.SetCurrentSlice(timeSlice.Slice);

                if (timeSlice.Slice.SymbolChangedEvents.Count != 0)
                {
                    if (hasOnDataSymbolChangedEvents)
                    {
                        methodInvokers[typeof (SymbolChangedEvents)](algorithm, timeSlice.Slice.SymbolChangedEvents);
                    }
                    foreach (var symbol in timeSlice.Slice.SymbolChangedEvents.Keys)
                    {
                        // cancel all orders for the old symbol
                        foreach (var ticket in transactions.GetOpenOrderTickets(x => x.Symbol == symbol))
                        {
                            ticket.Cancel("Open order cancelled on symbol changed event");
                        }
                    }
                }

                if (timeSlice.SecurityChanges != SecurityChanges.None)
                {
                    foreach (var security in timeSlice.SecurityChanges.AddedSecurities)
                    {
                        security.IsTradable = true;
                        // uses TryAdd, so don't need to worry about duplicates here
                        algorithm.Securities.Add(security);
                    }

                    var activeSecurities = algorithm.UniverseManager.ActiveSecurities;
                    foreach (var security in timeSlice.SecurityChanges.RemovedSecurities)
                    {
                        if (!activeSecurities.ContainsKey(security.Symbol))
                        {
                            security.IsTradable = false;
                        }
                    }

                    realtime.OnSecuritiesChanged(timeSlice.SecurityChanges);
                    results.OnSecuritiesChanged(timeSlice.SecurityChanges);
                }

                //Update the securities properties: first before calling user code to avoid issues with data
                foreach (var update in timeSlice.SecuritiesUpdateData)
                {
                    var security = update.Target;

                    security.Update(update.Data, update.DataType, update.ContainsFillForwardData);

                    if (!update.IsInternalConfig)
                    {
                        // Send market price updates to the TradeBuilder
                        algorithm.TradeBuilder.SetMarketPrice(security.Symbol, security.Price);
                    }
                }

                //Update the securities properties with any universe data
                if (timeSlice.UniverseData.Count > 0)
                {
                    foreach (var kvp in timeSlice.UniverseData)
                    {
                        foreach (var data in kvp.Value.Data)
                        {
                            Security security;
                            if (algorithm.Securities.TryGetValue(data.Symbol, out security))
                            {
                                security.Cache.StoreData(new[] {data}, data.GetType());
                            }
                        }
                    }
                }

                // poke each cash object to update from the recent security data
                foreach (var kvp in algorithm.Portfolio.CashBook)
                {
                    var cash = kvp.Value;
                    var updateData = cash.ConversionRateSecurity?.GetLastData();
                    if (updateData != null)
                    {
                        cash.Update(updateData);
                    }
                }
                // security prices got updated
                algorithm.Portfolio.InvalidateTotalPortfolioValue();

                // fire real time events after we've updated based on the new data
                realtime.SetTime(timeSlice.Time);

                // process fill models on the updated data before entering algorithm, applies to all non-market orders
                transactions.ProcessSynchronousEvents();

                // process end of day delistings
                ProcessDelistedSymbols(algorithm, delistings);

                // process split warnings for options
                ProcessSplitSymbols(algorithm, splitWarnings, delistings);

                //Check if the user's signalled Quit: loop over data until day changes.
                if (algorithm.Status == AlgorithmStatus.Stopped)
                {
                    Log.Trace("AlgorithmManager.Run(): Algorithm quit requested.");
                    break;
                }
                if (algorithm.RunTimeError != null)
                {
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Trace($"AlgorithmManager.Run(): Algorithm encountered a runtime error at {timeSlice.Time.ToStringInvariant()}. Error: {algorithm.RunTimeError}");
                    return;
                }

                // perform margin calls, in live mode we can also use realtime to emit these
                if (time >= nextMarginCallTime || (_liveMode && nextMarginCallTime > DateTime.UtcNow))
                {
                    // determine if there are possible margin call orders to be executed
                    bool issueMarginCallWarning;
                    var marginCallOrders = algorithm.Portfolio.MarginCallModel.GetMarginCallOrders(out issueMarginCallWarning);
                    if (marginCallOrders.Count != 0)
                    {
                        var executingMarginCall = false;
                        try
                        {
                            // tell the algorithm we're about to issue the margin call
                            algorithm.OnMarginCall(marginCallOrders);

                            executingMarginCall = true;

                            // execute the margin call orders
                            var executedTickets = algorithm.Portfolio.MarginCallModel.ExecuteMarginCall(marginCallOrders);
                            foreach (var ticket in executedTickets)
                            {
                                algorithm.Error($"{algorithm.Time.ToStringInvariant()} - Executed MarginCallOrder: {ticket.Symbol} - " +
                                    $"Quantity: {ticket.Quantity.ToStringInvariant()} @ {ticket.AverageFillPrice.ToStringInvariant()}"
                                );
                            }
                        }
                        catch (Exception err)
                        {
                            algorithm.RunTimeError = err;
                            _algorithm.Status = AlgorithmStatus.RuntimeError;
                            var locator = executingMarginCall ? "Portfolio.MarginCallModel.ExecuteMarginCall" : "OnMarginCall";
                            Log.Error($"AlgorithmManager.Run(): RuntimeError: {locator}: {err}");
                            return;
                        }
                    }
                    // we didn't perform a margin call, but got the warning flag back, so issue the warning to the algorithm
                    else if (issueMarginCallWarning)
                    {
                        try
                        {
                            algorithm.OnMarginCallWarning();
                        }
                        catch (Exception err)
                        {
                            algorithm.RunTimeError = err;
                            _algorithm.Status = AlgorithmStatus.RuntimeError;
                            Log.Error("AlgorithmManager.Run(): RuntimeError: OnMarginCallWarning: " + err);
                            return;
                        }
                    }

                    nextMarginCallTime = time + marginCallFrequency;
                }

                // perform check for settlement of unsettled funds
                if (time >= nextSettlementScanTime || (_liveMode && nextSettlementScanTime > DateTime.UtcNow))
                {
                    algorithm.Portfolio.ScanForCashSettlement(algorithm.UtcTime);

                    nextSettlementScanTime = time + settlementScanFrequency;
                }

                // before we call any events, let the algorithm know about universe changes
                if (timeSlice.SecurityChanges != SecurityChanges.None)
                {
                    try
                    {
                        var algorithmSecurityChanges = new SecurityChanges(timeSlice.SecurityChanges)
                        {
                            // by default for user code we want to filter out custom securities
                            FilterCustomSecurities = true
                        };

                        algorithm.OnSecuritiesChanged(algorithmSecurityChanges);
                        algorithm.OnFrameworkSecuritiesChanged(algorithmSecurityChanges);
                    }
                    catch (Exception err)
                    {
                        algorithm.RunTimeError = err;
                        _algorithm.Status = AlgorithmStatus.RuntimeError;
                        Log.Error("AlgorithmManager.Run(): RuntimeError: OnSecuritiesChanged event: " + err);
                        return;
                    }
                }

                // apply dividends
                foreach (var dividend in timeSlice.Slice.Dividends.Values)
                {
                    Log.Debug($"AlgorithmManager.Run(): {algorithm.Time}: Applying Dividend: {dividend}");

                    Security security = null;
                    if (_liveMode && algorithm.Securities.TryGetValue(dividend.Symbol, out security))
                    {
                        Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Pre-Dividend: {dividend}. " +
                            $"Security Holdings: {security.Holdings.Quantity} Account Currency Holdings: " +
                            $"{algorithm.Portfolio.CashBook[algorithm.AccountCurrency].Amount}");
                    }

                    var mode = algorithm.SubscriptionManager.SubscriptionDataConfigService
                        .GetSubscriptionDataConfigs(dividend.Symbol)
                        .DataNormalizationMode();

                    // apply the dividend event to the portfolio
                    algorithm.Portfolio.ApplyDividend(dividend, _liveMode, mode);

                    if (_liveMode && security != null)
                    {
                        Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Post-Dividend: {dividend}. Security " +
                            $"Holdings: {security.Holdings.Quantity} Account Currency Holdings: " +
                            $"{algorithm.Portfolio.CashBook[algorithm.AccountCurrency].Amount}");
                    }
                }

                // apply splits
                foreach (var split in timeSlice.Slice.Splits.Values)
                {
                    try
                    {
                        // only process split occurred events (ignore warnings)
                        if (split.Type != SplitType.SplitOccurred)
                        {
                            continue;
                        }

                        Log.Debug($"AlgorithmManager.Run(): {algorithm.Time}: Applying Split for {split.Symbol}");

                        Security security = null;
                        if (_liveMode && algorithm.Securities.TryGetValue(split.Symbol, out security))
                        {
                            Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Pre-Split for {split}. Security Price: {security.Price} Holdings: {security.Holdings.Quantity}");
                        }

                        var mode = algorithm.SubscriptionManager.SubscriptionDataConfigService
                            .GetSubscriptionDataConfigs(split.Symbol)
                            .DataNormalizationMode();

                        // apply the split event to the portfolio
                        algorithm.Portfolio.ApplySplit(split, _liveMode, mode);

                        if (_liveMode && security != null)
                        {
                            Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Post-Split for {split}. Security Price: {security.Price} Holdings: {security.Holdings.Quantity}");
                        }

                        // apply the split to open orders as well in raw mode, all other modes are split adjusted
                        if (_liveMode || mode == DataNormalizationMode.Raw)
                        {
                            // in live mode we always want to have our order match the order at the brokerage, so apply the split to the orders
                            var openOrders = transactions.GetOpenOrderTickets(ticket => ticket.Symbol == split.Symbol);
                            algorithm.BrokerageModel.ApplySplit(openOrders.ToList(), split);
                        }
                    }
                    catch (Exception err)
                    {
                        algorithm.RunTimeError = err;
                        _algorithm.Status = AlgorithmStatus.RuntimeError;
                        Log.Error("AlgorithmManager.Run(): RuntimeError: Split event: " + err);
                        return;
                    }
                }

                //Update registered consolidators for this symbol index
                try
                {
                    if (timeSlice.ConsolidatorUpdateData.Count > 0)
                    {
                        var timeKeeper = algorithm.TimeKeeper;
                        foreach (var update in timeSlice.ConsolidatorUpdateData)
                        {
                            var localTime = timeKeeper.GetLocalTimeKeeper(update.Target.ExchangeTimeZone).LocalTime;
                            var consolidators = update.Target.Consolidators;
                            foreach (var consolidator in consolidators)
                            {
                                foreach (var dataPoint in update.Data)
                                {
                                    // only push data into consolidators on the native, subscribed to resolution
                                    if (EndTimeIsInNativeResolution(update.Target, dataPoint.EndTime))
                                    {
                                        consolidator.Update(dataPoint);
                                    }
                                }

                                // scan for time after we've pumped all the data through for this consolidator
                                consolidator.Scan(localTime);
                            }
                        }
                    }
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: Consolidators update: " + err);
                    return;
                }

                // fire custom event handlers
                foreach (var update in timeSlice.CustomData)
                {
                    MethodInvoker methodInvoker;
                    if (!methodInvokers.TryGetValue(update.DataType, out methodInvoker))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var dataPoint in update.Data)
                        {
                            if (update.DataType.IsInstanceOfType(dataPoint))
                            {
                                methodInvoker(algorithm, dataPoint);
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        algorithm.RunTimeError = err;
                        _algorithm.Status = AlgorithmStatus.RuntimeError;
                        Log.Error("AlgorithmManager.Run(): RuntimeError: Custom Data: " + err);
                        return;
                    }
                }

                try
                {
                    // fire off the dividend and split events before pricing events
                    if (hasOnDataDividends && timeSlice.Slice.Dividends.Count != 0)
                    {
                        methodInvokers[typeof(Dividends)](algorithm, timeSlice.Slice.Dividends);
                    }
                    if (hasOnDataSplits && timeSlice.Slice.Splits.Count != 0)
                    {
                        methodInvokers[typeof(Splits)](algorithm, timeSlice.Slice.Splits);
                    }
                    if (hasOnDataDelistings && timeSlice.Slice.Delistings.Count != 0)
                    {
                        methodInvokers[typeof(Delistings)](algorithm, timeSlice.Slice.Delistings);
                    }
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: Dividends/Splits/Delistings: " + err);
                    return;
                }

                // run the delisting logic after firing delisting events
                HandleDelistedSymbols(algorithm, timeSlice.Slice.Delistings, delistings);

                // run split logic after firing split events
                HandleSplitSymbols(timeSlice.Slice.Splits, splitWarnings);

                //After we've fired all other events in this second, fire the pricing events:
                try
                {
                    if (hasOnDataTradeBars && timeSlice.Slice.Bars.Count > 0) methodInvokers[typeof(TradeBars)](algorithm, timeSlice.Slice.Bars);
                    if (hasOnDataQuoteBars && timeSlice.Slice.QuoteBars.Count > 0) methodInvokers[typeof(QuoteBars)](algorithm, timeSlice.Slice.QuoteBars);
                    if (hasOnDataOptionChains && timeSlice.Slice.OptionChains.Count > 0) methodInvokers[typeof(OptionChains)](algorithm, timeSlice.Slice.OptionChains);
                    if (hasOnDataTicks && timeSlice.Slice.Ticks.Count > 0) methodInvokers[typeof(Ticks)](algorithm, timeSlice.Slice.Ticks);
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: New Style Mode: " + err);
                    return;
                }

                try
                {
                    if (timeSlice.Slice.HasData)
                    {
                        // EVENT HANDLER v3.0 -- all data in a single event
                        algorithm.OnData(timeSlice.Slice);
                    }

                    // always turn the crank on this method to ensure universe selection models function properly on day changes w/out data
                    algorithm.OnFrameworkData(timeSlice.Slice);
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: Slice: " + err);
                    return;
                }

                //If its the historical/paper trading models, wait until market orders have been "filled"
                // Manually trigger the event handler to prevent thread switch.
                transactions.ProcessSynchronousEvents();

                // sample alpha charts now that we've updated time/price information and after transactions
                // are processed so that insights closed because of new order based insights get updated
                alphas.ProcessSynchronousEvents();

                // send the alpha statistics to the result handler for storage/transmit with the result packets
                results.SetAlphaRuntimeStatistics(alphas.RuntimeStatistics);

                // Process any required events of the results handler such as sampling assets, equity, or stock prices.
                results.ProcessSynchronousEvents();

                // poke the algorithm at the end of each time step
                algorithm.OnEndOfTimeStep();

            } // End of ForEach feed.Bridge.GetConsumingEnumerable

            // stop timing the loops
            TimeLimit.StopEnforcingTimeLimit();

            //Stream over:: Send the final packet and fire final events:
            Log.Trace("AlgorithmManager.Run(): Firing On End Of Algorithm...");
            try
            {
                algorithm.OnEndOfAlgorithm();
            }
            catch (Exception err)
            {
                _algorithm.Status = AlgorithmStatus.RuntimeError;
                algorithm.RunTimeError = new Exception("Error running OnEndOfAlgorithm(): " + err.Message, err.InnerException);
                Log.Error("AlgorithmManager.OnEndOfAlgorithm(): " + err);
                return;
            }

            // final processing now that the algorithm has completed
            alphas.ProcessSynchronousEvents();

            // send the final alpha statistics to the result handler for storage/transmit with the result packets
            results.SetAlphaRuntimeStatistics(alphas.RuntimeStatistics);

            // Process any required events of the results handler such as sampling assets, equity, or stock prices.
            results.ProcessSynchronousEvents(forceProcess: true);

            //Liquidate Holdings for Calculations:
            if (_algorithm.Status == AlgorithmStatus.Liquidated && _liveMode)
            {
                Log.Trace("AlgorithmManager.Run(): Liquidating algorithm holdings...");
                algorithm.Liquidate();
                results.LogMessage("Algorithm Liquidated");
                results.SendStatusUpdate(AlgorithmStatus.Liquidated);
            }

            //Manually stopped the algorithm
            if (_algorithm.Status == AlgorithmStatus.Stopped)
            {
                Log.Trace("AlgorithmManager.Run(): Stopping algorithm...");
                results.LogMessage("Algorithm Stopped");
                results.SendStatusUpdate(AlgorithmStatus.Stopped);
            }

            //Backtest deleted.
            if (_algorithm.Status == AlgorithmStatus.Deleted)
            {
                Log.Trace("AlgorithmManager.Run(): Deleting algorithm...");
                results.DebugMessage("Algorithm Id:(" + job.AlgorithmId + ") Deleted by request.");
                results.SendStatusUpdate(AlgorithmStatus.Deleted);
            }

            //Algorithm finished, send regardless of commands:
            results.SendStatusUpdate(AlgorithmStatus.Completed);
            SetStatus(AlgorithmStatus.Completed);

            //Take final samples:
            results.Sample(time, force: true);

        } // End of Run();

        /// <summary>
        /// Set the quit state.
        /// </summary>
        public void SetStatus(AlgorithmStatus state)
        {
            lock (_lock)
            {
                //We don't want anyone else to set our internal state to "Running".
                //This is controlled by the algorithm private variable only.
                //Algorithm could be null after it's initialized and they call Run on us
                if (state != AlgorithmStatus.Running && _algorithm != null)
                {
                    _algorithm.Status = state;
                }
            }
        }

        private IEnumerable<TimeSlice> Stream(IAlgorithm algorithm, ISynchronizer synchronizer, IResultHandler results, CancellationToken cancellationToken)
        {
            bool setStartTime = false;
            var timeZone = algorithm.TimeZone;
            var history = algorithm.HistoryProvider;

            // fulfilling history requirements of volatility models in live mode
            if (algorithm.LiveMode)
            {
                ProcessVolatilityHistoryRequirements(algorithm);
            }

            // get the required history job from the algorithm
            DateTime? lastHistoryTimeUtc = null;
            var historyRequests = algorithm.GetWarmupHistoryRequests().ToList();

            // initialize variables for progress computation
            var warmUpStartTicks = DateTime.UtcNow.Ticks;
            var nextStatusTime = DateTime.UtcNow.AddSeconds(1);
            var minimumIncrement = algorithm.UniverseManager
                .Select(x => x.Value.UniverseSettings?.Resolution.ToTimeSpan() ?? algorithm.UniverseSettings.Resolution.ToTimeSpan())
                .DefaultIfEmpty(Time.OneSecond)
                .Min();

            minimumIncrement = minimumIncrement == TimeSpan.Zero ? Time.OneSecond : minimumIncrement;

            if (historyRequests.Count != 0)
            {
                // rewrite internal feed requests
                var subscriptions = algorithm.SubscriptionManager.Subscriptions.Where(x => !x.IsInternalFeed).ToList();
                var minResolution = subscriptions.Count > 0 ? subscriptions.Min(x => x.Resolution) : Resolution.Second;
                foreach (var request in historyRequests)
                {
                    Security security;
                    if (algorithm.Securities.TryGetValue(request.Symbol, out security) && security.IsInternalFeed())
                    {
                        if (request.Resolution < minResolution)
                        {
                            request.Resolution = minResolution;
                            request.FillForwardResolution = request.FillForwardResolution.HasValue ? minResolution : (Resolution?) null;
                        }
                    }
                }

                // rewrite all to share the same fill forward resolution
                if (historyRequests.Any(x => x.FillForwardResolution.HasValue))
                {
                    minResolution = historyRequests.Where(x => x.FillForwardResolution.HasValue).Min(x => x.FillForwardResolution.Value);
                    foreach (var request in historyRequests.Where(x => x.FillForwardResolution.HasValue))
                    {
                        request.FillForwardResolution = minResolution;
                    }
                }

                foreach (var request in historyRequests)
                {
                    warmUpStartTicks = Math.Min(request.StartTimeUtc.Ticks, warmUpStartTicks);
                    Log.Trace($"AlgorithmManager.Stream(): WarmupHistoryRequest: {request.Symbol}: Start: {request.StartTimeUtc} End: {request.EndTimeUtc} Resolution: {request.Resolution}");
                }

                var timeSliceFactory = new TimeSliceFactory(timeZone);
                // make the history request and build time slices
                foreach (var slice in history.GetHistory(historyRequests, timeZone))
                {
                    TimeSlice timeSlice;
                    try
                    {
                        // we need to recombine this slice into a time slice
                        var paired = new List<DataFeedPacket>();
                        foreach (var symbol in slice.Keys)
                        {
                            var security = algorithm.Securities[symbol];
                            var data = slice[symbol];
                            var list = new List<BaseData>();
                            Type dataType;

                            var ticks = data as List<Tick>;
                            if (ticks != null)
                            {
                                list.AddRange(ticks);
                                dataType = typeof(Tick);
                            }
                            else
                            {
                                list.Add(data);
                                dataType = data.GetType();
                            }

                            var config = algorithm.SubscriptionManager.SubscriptionDataConfigService
                                .GetSubscriptionDataConfigs(symbol, includeInternalConfigs: true)
                                .FirstOrDefault(subscription => dataType.IsAssignableFrom(subscription.Type));

                            if (config == null)
                            {
                                throw new Exception($"A data subscription for type '{dataType.Name}' was not found.");
                            }
                            paired.Add(new DataFeedPacket(security, config, list));
                        }

                        timeSlice = timeSliceFactory.Create(slice.Time.ConvertToUtc(timeZone), paired, SecurityChanges.None, new Dictionary<Universe, BaseDataCollection>());
                    }
                    catch (Exception err)
                    {
                        Log.Error(err);
                        algorithm.RunTimeError = err;
                        yield break;
                    }

                    if (timeSlice != null)
                    {
                        if (!setStartTime)
                        {
                            setStartTime = true;
                            algorithm.Debug("Algorithm warming up...");
                        }
                        if (DateTime.UtcNow > nextStatusTime)
                        {
                            // send some status to the user letting them know we're done history, but still warming up,
                            // catching up to real time data
                            nextStatusTime = DateTime.UtcNow.AddSeconds(1);
                            var percent = (int)(100 * (timeSlice.Time.Ticks - warmUpStartTicks) / (double)(DateTime.UtcNow.Ticks - warmUpStartTicks));
                            results.SendStatusUpdate(AlgorithmStatus.History, $"Catching up to realtime {percent}%...");
                        }
                        yield return timeSlice;
                        lastHistoryTimeUtc = timeSlice.Time;
                    }
                }
            }

            // if we're not live or didn't event request warmup, then set us as not warming up
            if (!algorithm.LiveMode || historyRequests.Count == 0)
            {
                algorithm.SetFinishedWarmingUp();
                if (historyRequests.Count != 0)
                {
                    algorithm.Debug("Algorithm finished warming up.");
                    Log.Trace("AlgorithmManager.Stream(): Finished warmup");
                }
            }

            foreach (var timeSlice in synchronizer.StreamData(cancellationToken))
            {
                if (algorithm.LiveMode && algorithm.IsWarmingUp)
                {
                    if (timeSlice.IsTimePulse)
                    {
                        continue;
                    }

                    // this is hand-over logic, we spin up the data feed first and then request
                    // the history for warmup, so there will be some overlap between the data
                    if (lastHistoryTimeUtc.HasValue)
                    {
                        // make sure there's no historical data, this only matters for the handover
                        var hasHistoricalData = false;
                        foreach (var data in timeSlice.Slice.Ticks.Values.SelectMany(x => x).Concat<BaseData>(timeSlice.Slice.Bars.Values))
                        {
                            // check if any ticks in the list are on or after our last warmup point, if so, skip this data
                            if (data.EndTime.ConvertToUtc(algorithm.Securities[data.Symbol].Exchange.TimeZone) >= lastHistoryTimeUtc)
                            {
                                hasHistoricalData = true;
                                break;
                            }
                        }
                        if (hasHistoricalData)
                        {
                            continue;
                        }

                        // prevent us from doing these checks every loop
                        lastHistoryTimeUtc = null;
                    }

                    // in live mode wait to mark us as finished warming up when
                    // the data feed has caught up to now within the min increment
                    if (timeSlice.Time > DateTime.UtcNow.Subtract(minimumIncrement))
                    {
                        algorithm.SetFinishedWarmingUp();
                        algorithm.Debug("Algorithm finished warming up.");
                        Log.Trace("AlgorithmManager.Stream(): Finished warmup");
                    }
                    else if (DateTime.UtcNow > nextStatusTime)
                    {
                        // send some status to the user letting them know we're done history, but still warming up,
                        // catching up to real time data
                        nextStatusTime = DateTime.UtcNow.AddSeconds(1);
                        var percent = (int) (100*(timeSlice.Time.Ticks - warmUpStartTicks)/(double) (DateTime.UtcNow.Ticks - warmUpStartTicks));
                        results.SendStatusUpdate(AlgorithmStatus.History, $"Catching up to realtime {percent}%...");
                    }
                }
                yield return timeSlice;
            }
        }

        /// <summary>
        /// Helper method used to process securities volatility history requirements
        /// </summary>
        /// <remarks>Implemented as static to facilitate testing</remarks>
        /// <param name="algorithm">The algorithm instance</param>
        public static void ProcessVolatilityHistoryRequirements(IAlgorithm algorithm)
        {
            Log.Trace("ProcessVolatilityHistoryRequirements(): Updating volatility models with historical data...");

            foreach (var kvp in algorithm.Securities)
            {
                var security = kvp.Value;

                if (security.VolatilityModel != VolatilityModel.Null)
                {
                    // start: this is a work around to maintain retro compatibility
                    // did not want to add IVolatilityModel.SetSubscriptionDataConfigProvider
                    // to prevent breaking existing user models.
                    var baseType = security.VolatilityModel as BaseVolatilityModel;
                    baseType?.SetSubscriptionDataConfigProvider(
                        algorithm.SubscriptionManager.SubscriptionDataConfigService);
                    // end

                    var historyReq = security.VolatilityModel.GetHistoryRequirements(security, algorithm.UtcTime);

                    if (historyReq != null && algorithm.HistoryProvider != null)
                    {
                        var history = algorithm.HistoryProvider.GetHistory(historyReq, algorithm.TimeZone);
                        if (history != null)
                        {
                            foreach (var slice in history)
                            {
                                if (slice.Bars.ContainsKey(security.Symbol))
                                    security.VolatilityModel.Update(security, slice.Bars[security.Symbol]);
                            }
                        }
                    }
                }
            }

            Log.Trace("ProcessVolatilityHistoryRequirements(): finished.");
        }

        /// <summary>
        /// Adds a method invoker if the method exists to the method invokers dictionary
        /// </summary>
        /// <typeparam name="T">The data type to check for 'OnData(T data)</typeparam>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="methodInvokers">The dictionary of method invokers</param>
        /// <param name="methodName">The name of the method to search for</param>
        /// <returns>True if the method existed and was added to the collection</returns>
        private bool AddMethodInvoker<T>(IAlgorithm algorithm, Dictionary<Type, MethodInvoker> methodInvokers, string methodName = "OnData")
        {
            var newSplitMethodInfo = algorithm.GetType().GetMethod(methodName, new[] {typeof (T)});
            if (newSplitMethodInfo != null)
            {
                methodInvokers.Add(typeof(T), newSplitMethodInfo.DelegateForCallMethod());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Performs delisting logic for the securities specified in <paramref name="newDelistings"/> that are marked as <see cref="DelistingType.Delisted"/>.
        /// </summary>
        private static void HandleDelistedSymbols(IAlgorithm algorithm, Delistings newDelistings, List<Delisting> delistings)
        {
            foreach (var delisting in newDelistings.Values)
            {
                Log.Trace($"AlgorithmManager.HandleDelistedSymbols(): Delisting {delisting.Type}: {delisting.Symbol.Value}, UtcTime: {algorithm.UtcTime}, DelistingTime: {delisting.Time}");
                if (algorithm.LiveMode)
                {
                    // skip automatic handling of delisting event in live trading
                    // Lean will not exercise, liquidate or cancel open orders
                    continue;
                }

                // submit an order to liquidate on market close
                if (delisting.Type == DelistingType.Warning)
                {
                    if (delistings.All(x => x.Symbol != delisting.Symbol))
                    {
                        delistings.Add(delisting);
                    }
                }
                else
                {
                    // mark security as no longer tradable
                    var security = algorithm.Securities[delisting.Symbol];
                    security.IsTradable = false;
                    security.IsDelisted = true;

                    // the subscription are getting removed from the data feed because they end
                    // remove security from all universes
                    foreach (var ukvp in algorithm.UniverseManager)
                    {
                        var universe = ukvp.Value;
                        if (universe.ContainsMember(security.Symbol))
                        {
                            var userUniverse = universe as UserDefinedUniverse;
                            if (userUniverse != null)
                            {
                                userUniverse.Remove(security.Symbol);
                            }
                            else
                            {
                                universe.RemoveMember(algorithm.UtcTime, security);
                            }
                        }
                    }

                    var cancelledOrders = algorithm.Transactions.CancelOpenOrders(delisting.Symbol);
                    foreach (var cancelledOrder in cancelledOrders)
                    {
                        Log.Trace("AlgorithmManager.Run(): " + cancelledOrder);
                    }
                }
            }
        }

        /// <summary>
        /// Performs actual delisting of the contracts in delistings collection
        /// </summary>
        private static void ProcessDelistedSymbols(IAlgorithm algorithm, List<Delisting> delistings)
        {
            for (var i = delistings.Count - 1; i >= 0; i--)
            {
                var delisting = delistings[i];
                var security = algorithm.Securities[delisting.Symbol];
                if (security.Holdings.Quantity == 0)
                {
                    continue;
                }

                if (security.LocalTime < delisting.GetLiquidationTime(security.Exchange.Hours))
                {
                    continue;
                }

                // if there is any delisting event for a symbol that we are the underlying for and we are still invested retry
                // they will by liquidated first
                if (delistings.Any(delistingEvent => delistingEvent.Symbol.Underlying == security.Symbol
                    && algorithm.Securities[delistingEvent.Symbol].Invested))
                {
                    // this case could happen for example if we have a future 'A' position open and a future option position with underlying 'A'
                    // and both get delisted on the same date, we will allow the FOP exercise order to get handled first
                    continue;
                }

                var orderType = OrderType.Market;
                var tag = "Liquidate from delisting";
                if (security.Type == SecurityType.Option || security.Type == SecurityType.FutureOption)
                {
                    // tx handler will determine auto exercise/assignment
                    tag = "Option Expired";
                    orderType = OrderType.OptionExercise;
                }

                // submit an order to liquidate on market close or exercise (for options)
                var request = new SubmitOrderRequest(orderType, security.Type, security.Symbol,
                    -security.Holdings.Quantity, 0, 0, algorithm.UtcTime, tag);

                delistings.RemoveAt(i);
                algorithm.Transactions.ProcessRequest(request);

                // don't allow users to open a new position once we sent the liquidation order
                security.IsTradable = false;
            }
        }

        /// <summary>
        /// Keeps track of split warnings so we can later liquidate option contracts
        /// </summary>
        private void HandleSplitSymbols(Splits newSplits, List<Split> splitWarnings)
        {
            foreach (var split in newSplits.Values)
            {
                if (split.Type != SplitType.Warning)
                {
                    Log.Trace($"AlgorithmManager.HandleSplitSymbols(): {_algorithm.Time} - Security split occurred: Split Factor: {split} Reference Price: {split.ReferencePrice}");
                    continue;
                }

                Log.Trace($"AlgorithmManager.HandleSplitSymbols(): {_algorithm.Time} - Security split warning: {split}");

                if (!splitWarnings.Any(x => x.Symbol == split.Symbol && x.Type == SplitType.Warning))
                {
                    splitWarnings.Add(split);
                }
            }
        }

        /// <summary>
        /// Liquidate option contact holdings who's underlying security has split
        /// </summary>
        private void ProcessSplitSymbols(IAlgorithm algorithm, List<Split> splitWarnings, List<Delisting> delistings)
        {
            // NOTE: This method assumes option contracts have the same core trading hours as their underlying contract
            //       This is a small performance optimization to prevent scanning every contract on every time step,
            //       instead we scan just the underlyings, thereby reducing the time footprint of this methods by a factor
            //       of N, the number of derivative subscriptions
            for (int i = splitWarnings.Count - 1; i >= 0; i--)
            {
                var split = splitWarnings[i];
                var security = algorithm.Securities[split.Symbol];

                if (!security.IsTradable
                    && !algorithm.UniverseManager.ActiveSecurities.Keys.Contains(split.Symbol))
                {
                    Log.Debug($"AlgorithmManager.ProcessSplitSymbols(): {_algorithm.Time} - Removing split warning for {security.Symbol}");

                    // remove the warning from out list
                    splitWarnings.RemoveAt(i);
                    // Since we are storing the split warnings for a loop
                    // we need to check if the security was removed.
                    // When removed, it will be marked as non tradable but just in case
                    // we expect it not to be an active security either
                    continue;
                }

                var nextMarketClose = security.Exchange.Hours.GetNextMarketClose(security.LocalTime, false);

                // determine the latest possible time we can submit a MOC order
                var configs = algorithm.SubscriptionManager.SubscriptionDataConfigService
                    .GetSubscriptionDataConfigs(security.Symbol);

                if (configs.Count == 0)
                {
                    // should never happen at this point, if it does let's give some extra info
                    throw new Exception(
                        $"AlgorithmManager.ProcessSplitSymbols(): {_algorithm.Time} - No subscriptions found for {security.Symbol}" +
                        $", IsTradable: {security.IsTradable}" +
                        $", Active: {algorithm.UniverseManager.ActiveSecurities.Keys.Contains(split.Symbol)}");
                }

                var latestMarketOnCloseTimeRoundedDownByResolution = nextMarketClose.Subtract(MarketOnCloseOrder.DefaultSubmissionTimeBuffer)
                    .RoundDownInTimeZone(configs.GetHighestResolution().ToTimeSpan(), security.Exchange.TimeZone, configs.First().DataTimeZone);

                // we don't need to do anyhing until the market closes
                if (security.LocalTime < latestMarketOnCloseTimeRoundedDownByResolution) continue;

                // fetch all option derivatives of the underlying with holdings (excluding the canonical security)
                var derivatives = algorithm.Securities.Where(kvp => kvp.Key.HasUnderlying &&
                    (kvp.Key.SecurityType == SecurityType.Option || kvp.Key.SecurityType == SecurityType.FutureOption) &&
                    kvp.Key.Underlying == security.Symbol &&
                    !kvp.Key.Underlying.IsCanonical() &&
                    kvp.Value.HoldStock
                );

                foreach (var kvp in derivatives)
                {
                    var optionContractSymbol = kvp.Key;
                    var optionContractSecurity = (Option) kvp.Value;

                    if (delistings.Any(x => x.Symbol == optionContractSymbol
                        && x.Time.Date == optionContractSecurity.LocalTime.Date))
                    {
                        // if the option is going to be delisted today we skip sending the market on close order
                        continue;
                    }

                    // close any open orders
                    algorithm.Transactions.CancelOpenOrders(optionContractSymbol, "Canceled due to impending split. Separate MarketOnClose order submitted to liquidate position.");

                    var request = new SubmitOrderRequest(OrderType.MarketOnClose, optionContractSecurity.Type, optionContractSymbol,
                        -optionContractSecurity.Holdings.Quantity, 0, 0, algorithm.UtcTime,
                        "Liquidated due to impending split. Option splits are not currently supported."
                    );

                    // send MOC order to liquidate option contract holdings
                    algorithm.Transactions.AddOrder(request);

                    // mark option contract as not tradable
                    optionContractSecurity.IsTradable = false;

                    algorithm.Debug($"MarketOnClose order submitted for option contract '{optionContractSymbol}' due to impending {split.Symbol.Value} split event. "
                        + "Option splits are not currently supported.");
                }

                // remove the warning from out list
                splitWarnings.RemoveAt(i);
            }
        }

        /// <summary>
        /// Determines if a data point is in it's native, configured resolution
        /// </summary>
        private static bool EndTimeIsInNativeResolution(SubscriptionDataConfig config, DateTime dataPointEndTime)
        {
            if (config.Resolution == Resolution.Tick
                ||
                // time zones don't change seconds or milliseconds so we can
                // shortcut timezone conversions
                (config.Resolution == Resolution.Second
                || config.Resolution == Resolution.Minute)
                && dataPointEndTime.Ticks % config.Increment.Ticks == 0)
            {
                return true;
            }

            var roundedDataPointEndTime = dataPointEndTime.RoundDownInTimeZone(config.Increment, config.ExchangeTimeZone, config.DataTimeZone);
            return dataPointEndTime == roundedDataPointEndTime;
        }

        /// <summary>
        /// Constructs the correct <see cref="ITokenBucket"/> instance per the provided controls.
        /// The provided controls will be null when
        /// </summary>
        private static ITokenBucket CreateTokenBucket(LeakyBucketControlParameters controls)
        {
            if (controls == null)
            {
                // this will only be null when the AlgorithmManager is being initialized outside of LEAN
                // for example, in unit tests that don't provide a job package as well as from Research
                // in each of the above cases, it seems best to not enforce the leaky bucket restrictions
                return TokenBucket.Null;
            }

            Log.Trace("AlgorithmManager.CreateTokenBucket(): Initializing LeakyBucket: " +
                $"Capacity: {controls.Capacity} " +
                $"RefillAmount: {controls.RefillAmount} " +
                $"TimeInterval: {controls.TimeIntervalMinutes}"
            );

            // these parameters view 'minutes' as the resource being rate limited. the capacity is the total
            // number of minutes available for burst operations and after controls.TimeIntervalMinutes time
            // has passed, we'll add controls.RefillAmount to the 'minutes' available, maxing at controls.Capacity
            return new LeakyBucket(
                controls.Capacity,
                controls.RefillAmount,
                TimeSpan.FromMinutes(controls.TimeIntervalMinutes)
            );
        }
    }
}
