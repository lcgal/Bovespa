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
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NodaTime;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Alphas;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Tests.Common.Securities;
using QuantConnect.Util;
using HistoryRequest = QuantConnect.Data.HistoryRequest;

namespace QuantConnect.Tests
{
    /// <summary>
    /// Provides methods for running an algorithm and testing it's performance metrics
    /// </summary>
    public static class AlgorithmRunner
    {
        public static AlgorithmRunnerResults RunLocalBacktest(
            string algorithm,
            Dictionary<string, string> expectedStatistics,
            AlphaRuntimeStatistics expectedAlphaStatistics,
            Language language,
            AlgorithmStatus expectedFinalStatus,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string setupHandler = "RegressionSetupHandlerWrapper",
            decimal? initialCash = null)
        {
            AlgorithmManager algorithmManager = null;
            var statistics = new Dictionary<string, string>();
            var alphaStatistics = new AlphaRuntimeStatistics(new TestAccountCurrencyProvider());
            BacktestingResultHandler results = null;

            Composer.Instance.Reset();
            SymbolCache.Clear();

            var ordersLogFile = string.Empty;
            var logFile = $"./regression/{algorithm}.{language.ToLower()}.log";
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            File.Delete(logFile);

            try
            {
                // set the configuration up
                Config.Set("algorithm-type-name", algorithm);
                Config.Set("live-mode", "false");
                Config.Set("environment", "");
                Config.Set("messaging-handler", "QuantConnect.Messaging.Messaging");
                Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");
                Config.Set("setup-handler", setupHandler);
                Config.Set("history-provider", "RegressionHistoryProviderWrapper");
                Config.Set("api-handler", "QuantConnect.Api.Api");
                Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.RegressionResultHandler");
                Config.Set("algorithm-language", language.ToString());
                Config.Set("algorithm-location",
                    language == Language.Python
                        ? "../../../Algorithm.Python/" + algorithm + ".py"
                        : "QuantConnect.Algorithm." + language + ".dll");

                // Store initial log variables
                var initialLogHandler = Log.LogHandler;
                var initialDebugEnabled = Log.DebuggingEnabled;

                // Use our current test LogHandler and a FileLogHandler
                var newLogHandlers = new ILogHandler[] { MaintainLogHandlerAttribute.GetLogHandler(), new FileLogHandler(logFile, false) };

                using (Log.LogHandler = new CompositeLogHandler(newLogHandlers))
                using (var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance))
                using (var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance))
                using (var workerThread  = new TestWorkerThread())
                {
                    Log.DebuggingEnabled = true;

                    Log.Trace("");
                    Log.Trace("{0}: Running " + algorithm + "...", DateTime.UtcNow);
                    Log.Trace("");

                    // run the algorithm in its own thread
                    var engine = new Lean.Engine.Engine(systemHandlers, algorithmHandlers, false);
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            string algorithmPath;
                            var job = (BacktestNodePacket)systemHandlers.JobQueue.NextJob(out algorithmPath);
                            job.BacktestId = algorithm;
                            job.PeriodStart = startDate;
                            job.PeriodFinish = endDate;
                            if (initialCash.HasValue)
                            {
                                job.CashAmount = new CashAmount(initialCash.Value, Currencies.USD);
                            }
                            algorithmManager = new AlgorithmManager(false, job);

                            systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, job, algorithmManager);

                            engine.Run(job, algorithmManager, algorithmPath, workerThread);
                            ordersLogFile = ((RegressionResultHandler)algorithmHandlers.Results).LogFilePath;
                        }
                        catch (Exception e)
                        {
                            Log.Trace($"Error in AlgorithmRunner task: {e}");
                        }
                    }).Wait();

                    var backtestingResultHandler = (BacktestingResultHandler)algorithmHandlers.Results;
                    results = backtestingResultHandler;
                    statistics = backtestingResultHandler.FinalStatistics;

                    var defaultAlphaHandler = (DefaultAlphaHandler) algorithmHandlers.Alphas;
                    alphaStatistics = defaultAlphaHandler.RuntimeStatistics;
                }

                // Reset settings to initial values
                Log.LogHandler = initialLogHandler;
                Log.DebuggingEnabled = initialDebugEnabled;
            }
            catch (Exception ex)
            {
                if (expectedFinalStatus != AlgorithmStatus.RuntimeError)
                {
                    Log.Error("{0} {1}", ex.Message, ex.StackTrace);
                }
            }

            if (algorithmManager?.State != expectedFinalStatus)
            {
                Assert.Fail($"Algorithm state should be {expectedFinalStatus} and is: {algorithmManager?.State}");
            }

            foreach (var stat in expectedStatistics)
            {
                Assert.AreEqual(true, statistics.ContainsKey(stat.Key), "Missing key: " + stat.Key);
                Assert.AreEqual(stat.Value, statistics[stat.Key], "Failed on " + stat.Key);
            }

            if (expectedAlphaStatistics != null)
            {
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.MeanPopulationScore.Direction);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.MeanPopulationScore.Magnitude);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.RollingAveragedPopulationScore.Direction);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.RollingAveragedPopulationScore.Magnitude);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.LongShortRatio);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalInsightsClosed);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalInsightsGenerated);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalAccumulatedEstimatedAlphaValue);
                AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalInsightsAnalysisCompleted);
            }

            // we successfully passed the regression test, copy the log file so we don't have to continually
            // re-run master in order to compare against a passing run
            var passedFile = logFile.Replace("./regression/", "./passed/");
            Directory.CreateDirectory(Path.GetDirectoryName(passedFile));
            File.Delete(passedFile);
            File.Copy(logFile, passedFile);

            var passedOrderLogFile = ordersLogFile.Replace("./regression/", "./passed/");
            Directory.CreateDirectory(Path.GetDirectoryName(passedFile));
            File.Delete(passedOrderLogFile);
            if (File.Exists(ordersLogFile)) File.Copy(ordersLogFile, passedOrderLogFile);

            return new AlgorithmRunnerResults(algorithm, language, algorithmManager, results);
        }

        private static void AssertAlphaStatistics(AlphaRuntimeStatistics expected, AlphaRuntimeStatistics actual, Expression<Func<AlphaRuntimeStatistics, object>> selector)
        {
            // extract field name from expression
            var field = selector.AsEnumerable().OfType<MemberExpression>().First().ToString();
            field = field.Substring(field.IndexOf('.') + 1);

            var func = selector.Compile();
            var expectedValue = func(expected);
            var actualValue = func(actual);
            if (expectedValue is double)
            {
                Assert.AreEqual((double)expectedValue, (double)actualValue, 1e-4, "Failed on alpha statistics " + field);
            }
            else
            {
                Assert.AreEqual(expectedValue, actualValue, "Failed on alpha statistics " + field);
            }
        }

        /// <summary>
        /// Used to intercept the algorithm instance to aid the <see cref="RegressionHistoryProviderWrapper"/>
        /// </summary>
        public class RegressionSetupHandlerWrapper : BacktestingSetupHandler
        {
            public static IAlgorithm Algorithm { get; protected set; }
            public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath)
            {
                Algorithm = base.CreateAlgorithmInstance(algorithmNodePacket, assemblyPath);
                var framework = Algorithm as QCAlgorithm;
                if (framework != null)
                {
                    framework.DebugMode = true;
                }
                return Algorithm;
            }
        }

        /// <summary>
        /// Used to perform checks against history requests for all regression algorithms
        /// </summary>
        public class RegressionHistoryProviderWrapper : SubscriptionDataReaderHistoryProvider
        {
            public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
            {
                requests = requests.ToList();
                if (requests.Any(r => RegressionSetupHandlerWrapper.Algorithm.UniverseManager.ContainsKey(r.Symbol)))
                {
                    throw new Exception("History requests should not be submitted for universe symbols");
                }
                return base.GetHistory(requests, sliceTimeZone);
            }
        }

        public class TestWorkerThread : WorkerThread
        {
        }
    }
}
