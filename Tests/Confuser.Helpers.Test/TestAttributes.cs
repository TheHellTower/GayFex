using ApprovalTests.Reporters;
using ApprovalTests.Reporters.ContinuousIntegration;
using ApprovalTests.Reporters.TestFrameworks;
using ApprovalTests.Reporters.Windows;

[assembly: UseReporter(typeof(VisualStudioReporter), typeof(AppVeyorReporter), typeof(XUnit2Reporter))]