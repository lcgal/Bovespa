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
using NUnit.Framework;
using QuantConnect.Statistics;

namespace QuantConnect.Tests.Common.Statistics
{
    [TestFixture]
    public class StatisticsBuilderTests
    {
        [Test]
        public void MisalignedValues_ShouldThrow_DuringGeneration()
        {
            var testBenchmarkPoints = new List<ChartPoint>
            {
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 16, 0, 0), DateTimeKind.Utc), 100),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 16, 0, 0), DateTimeKind.Utc), 102),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 16, 0, 0), DateTimeKind.Utc), 110),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 4, 16, 0, 0), DateTimeKind.Utc), 110),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 5, 16, 0, 0), DateTimeKind.Utc), 120),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 16, 0, 0), DateTimeKind.Utc), 130),
            };

            var testEquityPoints = new List<ChartPoint>
            {
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2018, 12, 31, 16, 0, 0), DateTimeKind.Utc), 100000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 16, 0, 0), DateTimeKind.Utc), 100000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 16, 0, 0), DateTimeKind.Utc), 102000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 16, 0, 0), DateTimeKind.Utc), 110000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 4, 16, 0, 0), DateTimeKind.Utc), 110000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 5, 16, 0, 0), DateTimeKind.Utc), 120000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 16, 0, 0), DateTimeKind.Utc), 130000),
            };

            var misalignedTestPerformancePoints = new List<ChartPoint>
            {
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2018, 12, 31), DateTimeKind.Utc), 1000m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 16, 0, 0), DateTimeKind.Utc), 0.25m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 16, 0, 0), DateTimeKind.Utc), 0.02m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 16, 0, 0), DateTimeKind.Utc), 0.0784313725490196m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 4, 16, 0, 0), DateTimeKind.Utc), 0 * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 5, 16, 0, 0), DateTimeKind.Utc), 0.090909090909090m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 16, 0, 0), DateTimeKind.Utc), 0.083333333333333m * 100m)
            };

            Assert.Throws<Exception>(() =>
            {
                StatisticsBuilder.Generate(
                    new List<Trade>(),
                    new SortedDictionary<DateTime, decimal>(),
                    testEquityPoints,
                    misalignedTestPerformancePoints,
                    testBenchmarkPoints,
                    100000m,
                    0m,
                    1);
            }, "Misaligned values provided, but we still generate statistics");
        }

        [Test]
        public void Generate_HandlesMultipleEntriesPerDay_ResamplesProperly()
        {
            var testBenchmarkPoints = new List<ChartPoint>
            {
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 0, 0, 0), DateTimeKind.Utc), 0), // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 16, 0, 0), DateTimeKind.Utc), 100),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 16, 0, 0), DateTimeKind.Utc), 102),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 0, 0, 0), DateTimeKind.Utc), 0), // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 16, 0, 0), DateTimeKind.Utc), 110),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 4, 16, 0, 0), DateTimeKind.Utc), 110),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 5, 16, 0, 0), DateTimeKind.Utc), 120),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 16, 0, 0), DateTimeKind.Utc), 130),
            };

            var testEquityPoints = new List<ChartPoint>
            {
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 16, 0, 0), DateTimeKind.Utc), 100000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 16, 0, 0), DateTimeKind.Utc), 102000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 16, 0, 0), DateTimeKind.Utc), 110000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 4, 16, 0, 0), DateTimeKind.Utc), 110000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 5, 16, 0, 0), DateTimeKind.Utc), 120000),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 16, 0, 0), DateTimeKind.Utc), 130000),
            };

            var testPerformancePoints = new List<ChartPoint>
            {
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 9, 30, 0), DateTimeKind.Utc), 500000m * 100m),             // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 10, 30, 0), DateTimeKind.Utc), 1m * 100m),                 // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 11, 30, 0), DateTimeKind.Utc), 2m * 100m),                 // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 1, 16, 0, 0), DateTimeKind.Utc), 100000m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 4, 0, 0), DateTimeKind.Utc), 50m * 100m),                  // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 2, 16, 0, 0), DateTimeKind.Utc), 0.02m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 3, 16, 0, 0), DateTimeKind.Utc), 0.0784313725490196m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 4, 16, 0, 0), DateTimeKind.Utc), 0),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 5, 16, 0, 0), DateTimeKind.Utc), 0.090909090909090m * 100m),
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 0, 0, 0), DateTimeKind.Utc), 0m * 100m),                   // Should be resampled away
                new ChartPoint(DateTime.SpecifyKind(new DateTime(2019, 1, 6, 16, 0, 0), DateTimeKind.Utc), 0.083333333333333m * 100m)
            };

            var performance = StatisticsBuilder.Generate(
                new List<Trade>(),
                new SortedDictionary<DateTime, decimal>(),
                testEquityPoints,
                testPerformancePoints,
                testBenchmarkPoints,
                100000m,
                0m,
                1);

            Assert.AreEqual(1, Math.Round(performance.TotalPerformance.PortfolioStatistics.Beta, 5));
            Assert.AreEqual(0, performance.TotalPerformance.PortfolioStatistics.Drawdown);
        }
    }
}

