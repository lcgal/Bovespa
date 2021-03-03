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
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Test algorithm that reproduces GH issues 3410 and 3409.
    /// Coarse universe selection should start from the algorithm start date.
    /// Data returned by history requests performed from the selection method should be up to date.
    /// </summary>
    public class CoarseSelectionTimeRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _spy;
        private decimal _historyCoarseSpyPrice;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2014, 03, 25);
            SetEndDate(2014, 04, 01);

            _spy = AddEquity("SPY", Resolution.Daily).Symbol;

            UniverseSettings.Resolution = Resolution.Daily;
            AddUniverse(CoarseSelectionFunction);
        }

        public IEnumerable<Symbol> CoarseSelectionFunction(IEnumerable<CoarseFundamental> coarse)
        {
            var sortedByDollarVolume = coarse.OrderByDescending(x => x.DollarVolume);
            var top = sortedByDollarVolume
                .Where(fundamental => fundamental.Symbol != _spy) // ignore spy
                .Take(1);

            _historyCoarseSpyPrice = History(_spy, 1).First().Close;

            return top.Select(x => x.Symbol);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (data.Count != 2)
            {
                throw new Exception($"Unexpected data count: {data.Count}");
            }
            if (ActiveSecurities.Count != 2)
            {
                throw new Exception($"Unexpected ActiveSecurities count: {ActiveSecurities.Count}");
            }
            // the price obtained by the previous coarse selection should be the same as the current price
            if (_historyCoarseSpyPrice != 0 && _historyCoarseSpyPrice != Securities[_spy].Price)
            {
                throw new Exception($"Unexpected SPY price: {_historyCoarseSpyPrice}");
            }
            _historyCoarseSpyPrice = 0;
            if (!Portfolio.Invested)
            {
                SetHoldings(_spy, 1);
                Debug("Purchased Stock");
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
            {"Total Trades", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "58.336%"},
            {"Drawdown", "0.900%"},
            {"Expectancy", "0"},
            {"Net Profit", "1.012%"},
            {"Sharpe Ratio", "5.09"},
            {"Probabilistic Sharpe Ratio", "68.472%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0.322"},
            {"Beta", "0.265"},
            {"Annual Standard Deviation", "0.087"},
            {"Annual Variance", "0.008"},
            {"Information Ratio", "-0.088"},
            {"Tracking Error", "0.105"},
            {"Treynor Ratio", "1.667"},
            {"Total Fees", "$2.91"},
            {"Fitness Score", "0.141"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "9.731"},
            {"Return Over Maximum Drawdown", "61.515"},
            {"Portfolio Turnover", "0.143"},
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
            {"OrderListHash", "718d73fbddccb63aeacbf4659938b4b8"}
        };
    }
}
