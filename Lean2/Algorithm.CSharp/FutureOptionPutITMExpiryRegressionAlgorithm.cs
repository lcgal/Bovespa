/*
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
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This regression algorithm tests In The Money (ITM) future option expiry for puts.
    /// We expect 3 orders from the algorithm, which are:
    ///
    ///   * Initial entry, buy ES Put Option (expiring ITM) (buy, qty 1)
    ///   * Option exercise, receiving short ES future contracts (sell, qty -1)
    ///   * Future contract liquidation, due to impending expiry (buy qty 1)
    ///
    /// Additionally, we test delistings for future options and assert that our
    /// portfolio holdings reflect the orders the algorithm has submitted.
    /// </summary>
    public class FutureOptionPutITMExpiryRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _es19m20;
        private Symbol _esOption;
        private Symbol _expectedContract;

        public override void Initialize()
        {
            SetStartDate(2020, 1, 5);
            SetEndDate(2020, 6, 30);

            _es19m20 = AddFutureContract(
                QuantConnect.Symbol.CreateFuture(
                    Futures.Indices.SP500EMini,
                    Market.CME,
                    new DateTime(2020, 6, 19)),
                Resolution.Minute).Symbol;

            // Select a future option expiring ITM, and adds it to the algorithm.
            _esOption = AddFutureOptionContract(OptionChainProvider.GetOptionContractList(_es19m20, Time)
                .Where(x => x.ID.StrikePrice >= 3300m && x.ID.OptionRight == OptionRight.Put)
                .OrderBy(x => x.ID.StrikePrice)
                .Take(1)
                .Single(), Resolution.Minute).Symbol;

            _expectedContract = QuantConnect.Symbol.CreateOption(_es19m20, Market.CME, OptionStyle.American, OptionRight.Put, 3300m, new DateTime(2020, 6, 19));
            if (_esOption != _expectedContract)
            {
                throw new Exception($"Contract {_expectedContract} was not found in the chain");
            }

            Schedule.On(DateRules.Tomorrow, TimeRules.AfterMarketOpen(_es19m20, 1), () =>
            {
                MarketOrder(_esOption, 1);
            });
        }

        public override void OnData(Slice data)
        {
            // Assert delistings, so that we can make sure that we receive the delisting warnings at
            // the expected time. These assertions detect bug #4872
            foreach (var delisting in data.Delistings.Values)
            {
                if (delisting.Type == DelistingType.Warning)
                {
                    if (delisting.Time != new DateTime(2020, 6, 19))
                    {
                        throw new Exception($"Delisting warning issued at unexpected date: {delisting.Time}");
                    }
                }
                if (delisting.Type == DelistingType.Delisted)
                {
                    if (delisting.Time != new DateTime(2020, 6, 20))
                    {
                        throw new Exception($"Delisting happened at unexpected date: {delisting.Time}");
                    }
                }
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status != OrderStatus.Filled)
            {
                // There's lots of noise with OnOrderEvent, but we're only interested in fills.
                return;
            }

            if (!Securities.ContainsKey(orderEvent.Symbol))
            {
                throw new Exception($"Order event Symbol not found in Securities collection: {orderEvent.Symbol}");
            }

            var security = Securities[orderEvent.Symbol];
            if (security.Symbol == _es19m20)
            {
                AssertFutureOptionOrderExercise(orderEvent, security, Securities[_expectedContract]);
            }
            else if (security.Symbol == _expectedContract)
            {
                AssertFutureOptionContractOrder(orderEvent, security);
            }
            else
            {
                throw new Exception($"Received order event for unknown Symbol: {orderEvent.Symbol}");
            }

            Log($"{Time:yyyy-MM-dd HH:mm:ss} -- {orderEvent.Symbol} :: Price: {Securities[orderEvent.Symbol].Holdings.Price} Qty: {Securities[orderEvent.Symbol].Holdings.Quantity} Direction: {orderEvent.Direction} Msg: {orderEvent.Message}");
        }

        private void AssertFutureOptionOrderExercise(OrderEvent orderEvent, Security future, Security optionContract)
        {
            var expectedLiquidationTimeUtc = new DateTime(2020, 6, 19, 20, 0, 0);

            if (orderEvent.Direction == OrderDirection.Buy && future.Holdings.Quantity != 0)
            {
                // We expect the contract to have been liquidated immediately
                throw new Exception($"Did not liquidate existing holdings for Symbol {future.Symbol}");
            }
            if (orderEvent.Direction == OrderDirection.Buy && orderEvent.UtcTime != expectedLiquidationTimeUtc)
            {
                throw new Exception($"Liquidated future contract, but not at the expected time. Expected: {expectedLiquidationTimeUtc:yyyy-MM-dd HH:mm:ss} - found {orderEvent.UtcTime:yyyy-MM-dd HH:mm:ss}");
            }

            // No way to detect option exercise orders or any other kind of special orders
            // other than matching strings, for now.
            if (orderEvent.Message.Contains("Option Exercise"))
            {
                if (orderEvent.FillPrice != 3300m)
                {
                    throw new Exception("Option did not exercise at expected strike price (3300)");
                }
                if (future.Holdings.Quantity != -1)
                {
                    // Here, we expect to have some holdings in the underlying, but not in the future option anymore.
                    throw new Exception($"Exercised option contract, but we have no holdings for Future {future.Symbol}");
                }

                if (optionContract.Holdings.Quantity != 0)
                {
                    throw new Exception($"Exercised option contract, but we have holdings for Option contract {optionContract.Symbol}");
                }
            }
        }

        private void AssertFutureOptionContractOrder(OrderEvent orderEvent, Security option)
        {
            if (orderEvent.Direction == OrderDirection.Buy && option.Holdings.Quantity != 1)
            {
                throw new Exception($"No holdings were created for option contract {option.Symbol}");
            }
            if (orderEvent.Direction == OrderDirection.Sell && option.Holdings.Quantity != 0)
            {
                throw new Exception($"Holdings were found after a filled option exercise");
            }
            if (orderEvent.Message.Contains("Exercise") && option.Holdings.Quantity != 0)
            {
                throw new Exception($"Holdings were found after exercising option contract {option.Symbol}");
            }
        }

        /// <summary>
        /// Ran at the end of the algorithm to ensure the algorithm has no holdings
        /// </summary>
        /// <exception cref="Exception">The algorithm has holdings</exception>
        public override void OnEndOfAlgorithm()
        {
            if (Portfolio.Invested)
            {
                throw new Exception($"Expected no holdings at end of algorithm, but are invested in: {string.Join(", ", Portfolio.Keys)}");
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "3"},
            {"Average Win", "4.15%"},
            {"Average Loss", "-8.27%"},
            {"Compounding Annual Return", "-9.486%"},
            {"Drawdown", "4.500%"},
            {"Expectancy", "-0.249"},
            {"Net Profit", "-4.457%"},
            {"Sharpe Ratio", "-1.412"},
            {"Probabilistic Sharpe Ratio", "0.002%"},
            {"Loss Rate", "50%"},
            {"Win Rate", "50%"},
            {"Profit-Loss Ratio", "0.50"},
            {"Alpha", "-0.076"},
            {"Beta", "-0.002"},
            {"Annual Standard Deviation", "0.053"},
            {"Annual Variance", "0.003"},
            {"Information Ratio", "0.871"},
            {"Tracking Error", "0.183"},
            {"Treynor Ratio", "37.798"},
            {"Total Fees", "$7.40"},
            {"Fitness Score", "0.008"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "-0.238"},
            {"Return Over Maximum Drawdown", "-2.128"},
            {"Portfolio Turnover", "0.024"},
            {"Total Insights Generated", "0"},
            {"Total Insights Closed", "0"},
            {"Total Insights Analysis Completed", "0"},
            {"Long Insight Count", "0"},
            {"Short Insight Count", "0"},
            {"Long/Short Ratio", "100%"},
            {"Estimated Monthly Alpha Value", "$0"},
            {"Total Accumulated Estimated Alpha Value", "$0"},
            {"Mean Population Estimated Insight Value", "$0"},
            {"Mean Population Direction", "0%"},
            {"Mean Population Magnitude", "0%"},
            {"Rolling Averaged Population Direction", "0%"},
            {"Rolling Averaged Population Magnitude", "0%"},
            {"OrderListHash", "d3fa88c3acadb9345ceac76a2dd3b520"}
        };
    }
}

