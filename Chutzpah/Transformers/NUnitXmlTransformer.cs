﻿using System;
using System.Linq;
using System.Text;
using Chutzpah.Models;
using Encoder = Microsoft.Security.Application.Encoder;
using Chutzpah.Wrappers;
using System.IO;
using System.Xml;

namespace Chutzpah.Transformers
{
    public class NUnit2XmlTransformer : SummaryTransformer
    {
        public override string Name
        {
            get { return "nunit2"; }
        }

        public override string Description
        {
            get { return "output results to NUnit-style XML file"; }
        }

        public NUnit2XmlTransformer(IFileSystemWrapper fileSystem)
            : base(fileSystem)
        {

        }

        private XmlElement AddTestResultsRoot(TestCaseSummary summary, XmlDocument document)
        {
            var testResults = document.CreateElement("test-results");
            testResults.SetAttribute("name", "Chutzpah Test Results");

            testResults.SetAttribute("total", summary.TotalCount.ToString());
            testResults.SetAttribute("errors", summary.Errors.Count.ToString());
            testResults.SetAttribute("failures", summary.FailedCount.ToString());
            testResults.SetAttribute("not-run", (summary.TotalCount - summary.Errors.Count - summary.FailedCount - summary.PassedCount).ToString());
            testResults.SetAttribute("inconclusive", "0");
            testResults.SetAttribute("ignored", "0");
            testResults.SetAttribute("skipped", (summary.TotalCount - summary.Errors.Count - summary.FailedCount - summary.PassedCount).ToString());
            testResults.SetAttribute("invalid", "0");
            testResults.SetAttribute("date", DateTime.Now.ToString("yyyy-MM-dd"));
            testResults.SetAttribute("time", DateTime.Now.ToString("HH:mm:ss"));

            document.AppendChild(testResults);
            return testResults;
        }

        private void AddFailureToTestCase(TestCase test, XmlElement testCaseElement, XmlDocument document)
        {
            if (test.Passed == false)
            {
                var failure = document.CreateElement("failure");
                var failureMessage = document.CreateElement("message");
                var stack = document.CreateElement("stack-trace");

                failure.AppendChild(failureMessage);
                failure.AppendChild(stack);

                var testFailure = test.TestResults.First(tr => tr.Passed == false);
                failureMessage.InnerText = testFailure.GetFailureMessage();
                stack.InnerText = testFailure.StackTrace;

                var reason = document.CreateElement("reason");
                var reasonMessage = document.CreateElement("message");
                reason.AppendChild(reasonMessage);
                reasonMessage.InnerText = testFailure.Message;

                testCaseElement.AppendChild(failure);
                testCaseElement.AppendChild(reason);
            }
        }

        private XmlElement AddTestCase(TestCase test, XmlElement results, XmlDocument document)
        {
            var testCase = document.CreateElement("test-case");
            testCase.SetAttribute("name", test.TestName);
            testCase.SetAttribute("description", test.GetDisplayName());
            testCase.SetAttribute("success", test.Passed ? "True" : "False");
            testCase.SetAttribute("time", (test.TimeTaken / 1000m).ToString());
            testCase.SetAttribute("executed", "True");
            testCase.SetAttribute("asserts", "0");
            testCase.SetAttribute("result", test.Passed ? "Success" : "Fail");

            AddFailureToTestCase(test, testCase, document);

            results.AppendChild(testCase);

            return testCase;
        }

        public override string Transform(TestCaseSummary testFileSummary)
        {
            if (testFileSummary == null) throw new ArgumentNullException("testFileSummary");

            var document = new System.Xml.XmlDocument();
            var testResults = AddTestResultsRoot(testFileSummary, document);
        
            foreach (var testFile in testFileSummary.TestFileSummaries)
            {
                var testSuite = document.CreateElement("test-suite");
                testSuite.SetAttribute("type", "JavaScript");
                testSuite.SetAttribute("name", testFile.Path);
                testSuite.SetAttribute("succcess", testFile.PassedCount.ToString());
                testSuite.SetAttribute("time", (testFile.TimeTaken / 1000m).ToString());
                testSuite.SetAttribute("executed", testFile.PassedCount + testFile.FailedCount == testFile.TotalCount ? "True" : "False");
                testSuite.SetAttribute("asserts", "0");
                testSuite.SetAttribute("result", testFile.PassedCount == testFile.TotalCount ? "Success" : "Failed");
                testResults.AppendChild(testSuite);

                var results = document.CreateElement("results");
                testSuite.AppendChild(results);

                foreach (var test in testFile.Tests)
                {
                    AddTestCase(test, results, document);
                }
            }

            var stream = new MemoryStream();
            var xmlWriter = XmlTextWriter.Create(stream, new XmlWriterSettings { Indent = true, Encoding = System.Text.ASCIIEncoding.ASCII });

            document.Save(xmlWriter);
            return Encoding.ASCII.GetString(stream.ToArray());
        }

        private static string Encode(string str)
        {
            return Encoder.XmlEncode(str);
        }
    }
}