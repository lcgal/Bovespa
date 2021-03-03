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
using QuantConnect.Data;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This algorithm is a test case for adding forex symbols at a higher resolution of an existing internal feed.
    /// The second symbol is added in the OnData method.
    /// </summary>
    public class ForexInternalFeedOnDataHigherResolutionRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private readonly Dictionary<Symbol, int> _dataPointsPerSymbol = new Dictionary<Symbol, int>();
        private bool _added;
        private Symbol _eurusd;
        private DateTime lastDataTime = DateTime.MinValue;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 8);
            SetCash(100000);

            _eurusd = QuantConnect.Symbol.Create("EURUSD", SecurityType.Forex, Market.Oanda);
            var eurgbp = AddForex("EURGBP", Resolution.Daily);
            _dataPointsPerSymbol.Add(eurgbp.Symbol, 0);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (lastDataTime == data.Time)
            {
                throw new Exception("Duplicate time for current data and last data slice");
            }

            lastDataTime = data.Time;

            if (_added)
            {
                var eurUsdSubscription = SubscriptionManager.SubscriptionDataConfigService
                    .GetSubscriptionDataConfigs(_eurusd, includeInternalConfigs:true)
                    .Single();
                if (eurUsdSubscription.IsInternalFeed)
                {
                    throw new Exception("Unexpected internal 'EURUSD' Subscription");
                }
            }
            if (!_added)
            {
                var eurUsdSubscription = SubscriptionManager.SubscriptionDataConfigService
                    .GetSubscriptionDataConfigs(_eurusd, includeInternalConfigs: true)
                    .Single();
                if (!eurUsdSubscription.IsInternalFeed)
                {
                    throw new Exception("Unexpected not internal 'EURUSD' Subscription");
                }
                AddForex("EURUSD", Resolution.Hour);
                _dataPointsPerSymbol.Add(_eurusd, 0);

                _added = true;
            }

            foreach (var kvp in data)
            {
                var symbol = kvp.Key;
                _dataPointsPerSymbol[symbol]++;

                Log($"{Time} {symbol.Value} {kvp.Value.Price} EndTime {kvp.Value.EndTime}");
            }
        }

        /// <summary>
        /// End of algorithm run event handler. This method is called at the end of a backtest or live trading operation. Intended for closing out logs.
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            // EURUSD has only one day of hourly data, because it was added on the first time step instead of during Initialize
            var expectedDataPointsPerSymbol = new Dictionary<string, int>
            {
                { "EURGBP", 3 },
                { "EURUSD", 28 }
            };

            foreach (var kvp in _dataPointsPerSymbol)
            {
                var symbol = kvp.Key;
                var actualDataPoints = _dataPointsPerSymbol[symbol];
                Log($"Data points for symbol {symbol.Value}: {actualDataPoints}");

                if (actualDataPoints != expectedDataPointsPerSymbol[symbol.Value])
                {
                    throw new Exception($"Data point count mismatch for symbol {symbol.Value}: expected: {expectedDataPointsPerSymbol[symbol.Value]}, actual: {actualDataPoints}");
                }
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "0"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "0%"},
            {"Drawdown", "0%"},
            {"Expectancy", "0"},
            {"Net Profit", "0%"},
            {"Sharpe Ratio", "0"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "5.853"},
            {"Tracking Error", "0.107"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$0.00"},
            {"Fitness Score", "0"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "79228162514264337593543950335"},
            {"Return Over Maximum Drawdown", "79228162514264337593543950335"},
            {"Portfolio Turnover", "0"},
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
            {"OrderListHash", "d41d8cd98f00b204e9800998ecf8427e"}
        };
    }
}
