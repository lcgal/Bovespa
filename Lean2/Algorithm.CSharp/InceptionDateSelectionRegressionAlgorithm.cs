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
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm to test universe additions and removals with open positions
    /// </summary>
    /// <meta name="tag" content="regression test" />
    public class InceptionDateSelectionRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private SecurityChanges _changes = SecurityChanges.None;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 1);
            SetEndDate(2013, 10, 31);
            SetCash(100000);

            UniverseSettings.Resolution = Resolution.Hour;

            // select IBM once a week, empty universe the other days
            AddUniverseSelection(new CustomUniverseSelectionModel("my-custom-universe", dt => dt.Day % 7 == 0 ? new List<string> { "IBM" } : Enumerable.Empty<string>()));
            // Adds SPY 5 days after StartDate and keep it in Universe
            AddUniverseSelection(new InceptionDateUniverseSelectionModel("spy-inception", new Dictionary<string, DateTime> {{"SPY", StartDate.AddDays(5)}}));
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars dictionary object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (_changes == SecurityChanges.None) return;

            // we'll simply go long each security we added to the universe
            foreach (var security in _changes.AddedSecurities)
            {
                SetHoldings(security.Symbol, .5);
            }

            _changes = SecurityChanges.None;
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="changes">Object containing AddedSecurities and RemovedSecurities</param>
        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            // liquidate securities removed from our universe
            foreach (var security in changes.RemovedSecurities)
            {
                Liquidate(security.Symbol, "Removed from Universe");
            }

            _changes = changes;
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
            {"Total Trades", "9"},
            {"Average Win", "0.11%"},
            {"Average Loss", "-0.24%"},
            {"Compounding Annual Return", "28.263%"},
            {"Drawdown", "1.200%"},
            {"Expectancy", "-0.265"},
            {"Net Profit", "2.113%"},
            {"Sharpe Ratio", "4.037"},
            {"Probabilistic Sharpe Ratio", "77.550%"},
            {"Loss Rate", "50%"},
            {"Win Rate", "50%"},
            {"Profit-Loss Ratio", "0.47"},
            {"Alpha", "0.018"},
            {"Beta", "0.477"},
            {"Annual Standard Deviation", "0.068"},
            {"Annual Variance", "0.005"},
            {"Information Ratio", "-3.604"},
            {"Tracking Error", "0.073"},
            {"Treynor Ratio", "0.573"},
            {"Total Fees", "$14.75"},
            {"Fitness Score", "0.2"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "9.401"},
            {"Return Over Maximum Drawdown", "38.513"},
            {"Portfolio Turnover", "0.203"},
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
            {"OrderListHash", "599d18b0751e5efc95116bab44beeb97"}
        };
    }
}
