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
using System.Collections.Generic;
using System.Linq;
using Deedle;
using Python.Runtime;
using QuantConnect.Orders;
using QuantConnect.Packets;

namespace QuantConnect.Report.ReportElements
{
    internal sealed class LeverageUtilizationReportElement : ChartReportElement
    {
        private LiveResult _live;
        private BacktestResult _backtest;
        private List<PointInTimePortfolio> _backtestPortfolios;
        private List<PointInTimePortfolio> _livePortfolios;

        /// <summary>
        /// Create a new plot of the leverage utilization
        /// </summary>
        /// <param name="name">Name of the widget</param>
        /// <param name="key">Location of injection</param>
        /// <param name="backtest">Backtest result object</param>
        /// <param name="live">Live result object</param>
        /// <param name="backtestPortfolios">Backtest point in time portfolios</param>
        /// <param name="livePortfolios">Live point in time portfolios</param>
        public LeverageUtilizationReportElement(
            string name,
            string key,
            BacktestResult backtest,
            LiveResult live,
            List<PointInTimePortfolio> backtestPortfolios,
            List<PointInTimePortfolio> livePortfolios)
        {
            _backtest = backtest;
            _backtestPortfolios = backtestPortfolios;
            _live = live;
            _livePortfolios = livePortfolios;
            Name = name;
            Key = key;
        }

        /// <summary>
        /// Generate the leverage utilization plot using the python libraries.
        /// </summary>
        public override string Render()
        {
            var backtestSeries = Metrics.LeverageUtilization(_backtestPortfolios).FillMissing(Direction.Forward);
            var liveSeries = Metrics.LeverageUtilization(_livePortfolios).FillMissing(Direction.Forward);

            var base64 = "";
            using (Py.GIL())
            {
                var backtestList = new PyList();
                var liveList = new PyList();

                backtestList.Append(backtestSeries.Keys.ToList().ToPython());
                backtestList.Append(backtestSeries.Values.ToList().ToPython());

                liveList.Append(liveSeries.Keys.ToList().ToPython());
                liveList.Append(liveSeries.Values.ToList().ToPython());

                base64 = Charting.GetLeverage(backtestList, liveList);
            }

            return base64;
        }
    }
}