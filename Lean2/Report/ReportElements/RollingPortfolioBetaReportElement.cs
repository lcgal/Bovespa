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
*/

using System;
using System.Linq;
using Deedle;
using Python.Runtime;
using QuantConnect.Packets;

namespace QuantConnect.Report.ReportElements
{
    internal sealed class RollingPortfolioBetaReportElement : ChartReportElement
    {
        private LiveResult _live;
        private BacktestResult _backtest;

        /// <summary>
        /// Create a new plot of the rolling portfolio beta to equities
        /// </summary>
        /// <param name="name">Name of the widget</param>
        /// <param name="key">Location of injection</param>
        /// <param name="backtest">Backtest result object</param>
        /// <param name="live">Live result object</param>
        public RollingPortfolioBetaReportElement(string name, string key, BacktestResult backtest, LiveResult live)
        {
            _live = live;
            _backtest = backtest;
            Name = name;
            Key = key;
        }

        /// <summary>
        /// Generate the rolling portfolio beta to equities plot using the python libraries.
        /// </summary>
        public override string Render()
        {
            var backtestPoints = ResultsUtil.EquityPoints(_backtest);
            var backtestBenchmarkPoints = ResultsUtil.BenchmarkPoints(_backtest);
            var livePoints = ResultsUtil.EquityPoints(_live);
            var liveBenchmarkPoints = ResultsUtil.BenchmarkPoints(_live);

            var backtestSeries = new Series<DateTime, double>(backtestPoints);
            var backtestBenchmarkSeries = new Series<DateTime, double>(backtestBenchmarkPoints);
            var liveSeries = new Series<DateTime, double>(livePoints);
            var liveBenchmarkSeries = new Series<DateTime, double>(liveBenchmarkPoints);

            var base64 = "";
            using (Py.GIL())
            {
                var backtestList = new PyList();
                var liveList = new PyList();

                var backtestRollingBetaSixMonths = Rolling.Beta(backtestSeries, backtestBenchmarkSeries, windowSize: 22 * 6);
                var backtestRollingBetaTwelveMonths = Rolling.Beta(backtestSeries, backtestBenchmarkSeries, windowSize: 252);

                backtestList.Append(backtestRollingBetaSixMonths.Keys.ToList().ToPython());
                backtestList.Append(backtestRollingBetaSixMonths.Values.ToList().ToPython());
                backtestList.Append(backtestRollingBetaTwelveMonths.Keys.ToList().ToPython());
                backtestList.Append(backtestRollingBetaTwelveMonths.Values.ToList().ToPython());

                var liveRollingBetaSixMonths = Rolling.Beta(liveSeries, liveBenchmarkSeries, windowSize: 22 * 6);
                var liveRollingBetaTwelveMonths = Rolling.Beta(liveSeries, liveBenchmarkSeries, windowSize: 252);

                liveList.Append(liveRollingBetaSixMonths.Keys.ToList().ToPython());
                liveList.Append(liveRollingBetaSixMonths.Values.ToList().ToPython());
                liveList.Append(liveRollingBetaTwelveMonths.Keys.ToList().ToPython());
                liveList.Append(liveRollingBetaTwelveMonths.Values.ToList().ToPython());

                base64 = Charting.GetRollingBeta(backtestList, liveList);
            }

            return base64;
        }
    }
}