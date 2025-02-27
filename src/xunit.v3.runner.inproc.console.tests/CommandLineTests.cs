using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;
using Xunit.Runner.Common;
using Xunit.Runner.InProc.SystemConsole;
using Xunit.v3;

public class CommandLineTests
{
	public class UnknownOption
	{
		[Fact]
		public static void UnknownOptionThrows()
		{
			var commandLine = new TestableCommandLine("-unknown");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("unknown option: -unknown", exception.Message);
		}
	}

	public class Project
	{
		[Fact]
		public static void DefaultValues()
		{
			var commandLine = new TestableCommandLine();

			var project = commandLine.Parse();

			var assembly = Assert.Single(project.Assemblies);
			Assert.Equal($"/full/path/{typeof(CommandLineTests).Assembly.Location}", assembly.AssemblyFileName);
			Assert.Null(assembly.ConfigFileName);
		}

		[Fact]
		public static void ConfigFileDoesNotExist_Throws()
		{
			var commandLine = new TestableCommandLine("badConfig.json");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("config file not found: badConfig.json", exception.Message);
		}

		[Fact]
		public static void ConfigFileUnsupportedFormat_Throws()
		{
			var commandLine = new TestableCommandLine("assembly1.config");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("unknown option: assembly1.config", exception.Message);
		}

		[Fact]
		public static void TwoConfigFiles_Throws()
		{
			var commandLine = new TestableCommandLine("assembly1.json", "assembly2.json");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("unknown option: assembly2.json", exception.Message);
		}

		[Fact]
		public static void WithConfigFile()
		{
			var commandLine = new TestableCommandLine("assembly1.json");

			var project = commandLine.Parse();

			var assembly = Assert.Single(project.Assemblies);
			Assert.Equal("/full/path/assembly1.json", assembly.ConfigFileName);
		}
	}

	public class Switches
	{
		static readonly (string Switch, Expression<Func<XunitProject, bool>> Accessor)[] SwitchOptionsList = new (string, Expression<Func<XunitProject, bool>>)[]
		{
			("-debug", project => project.Configuration.DebugOrDefault),
			("-diagnostics", project => project.Assemblies.All(a => a.Configuration.DiagnosticMessagesOrDefault)),
			("-failskips", project => project.Assemblies.All(a => a.Configuration.FailSkipsOrDefault)),
			("-ignorefailures", project => project.Configuration.IgnoreFailuresOrDefault),
			("-internaldiagnostics", project => project.Assemblies.All(a => a.Configuration.InternalDiagnosticMessagesOrDefault)),
			("-noautoreporters", project => project.Configuration.NoAutoReportersOrDefault),
			("-nocolor", project => project.Configuration.NoColorOrDefault),
			("-nologo", project => project.Configuration.NoLogoOrDefault),
			("-pause", project => project.Configuration.PauseOrDefault),
			("-preenumeratetheories", project => project.Assemblies.All(a => a.Configuration.PreEnumerateTheories ?? false)),
			("-stoponfail", project => project.Assemblies.All(a => a.Configuration.StopOnFailOrDefault)),
			("-wait", project => project.Configuration.WaitOrDefault),
		};

		public static readonly TheoryData<string, Expression<Func<XunitProject, bool>>> SwitchesLowerCase =
			new(SwitchOptionsList);

		public static readonly TheoryData<string, Expression<Func<XunitProject, bool>>> SwitchesUpperCase =
			new(SwitchOptionsList.Select(t => (t.Switch.ToUpperInvariant(), t.Accessor)));

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void SwitchDefault(
			string _,
			Expression<Func<XunitProject, bool>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json");
			var project = commandLine.Parse();

			var result = accessor.Compile().Invoke(project);

			Assert.False(result);
		}

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void SwitchOverride(
			string @switch,
			Expression<Func<XunitProject, bool>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch);
			var project = commandLine.Parse();

			var result = accessor.Compile().Invoke(project);

			Assert.True(result);
		}
	}

	public class OptionsWithArguments
	{
		public class Culture
		{
			[Fact]
			public static void DefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Null(assembly.Configuration.Culture);
			}

			[Fact]
			public static void ExplicitDefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-culture", "default");

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Null(assembly.Configuration.Culture);
			}

			[Fact]
			public static void InvariantCultureIsEmptyString()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-culture", "invariant");

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Equal(string.Empty, assembly.Configuration.Culture);
			}

			[Fact]
			public static void ValueIsPreserved()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-culture", "foo");

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Equal("foo", assembly.Configuration.Culture);
			}
		}

		public class MaxThreads
		{
			[Fact]
			public static void DefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Null(assembly.Configuration.MaxParallelThreads);
			}

			[Fact]
			public static void MissingValue()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads");

				var exception = Record.Exception(() => commandLine.Parse());

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal("missing argument for -maxthreads", exception.Message);
			}

			[Theory]
			[InlineData("abc")]
			[InlineData("0.ax")]  // Non-digit
			[InlineData(".0x")]   // Missing leading digit
			public static void InvalidValues(string value)
			{
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads", value);

				var exception = Record.Exception(() => commandLine.Parse());

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal("incorrect argument value for -maxthreads (must be 'default', 'unlimited', a positive number, or a multiplier in the form of '0.0x')", exception.Message);
			}

			[Theory]
			[InlineData("default", null)]
			[InlineData("0", null)]
			[InlineData("unlimited", -1)]
			[InlineData("16", 16)]
			public static void ValidValues(
				string value,
				int? expected)
			{
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads", value);

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Equal(expected, assembly.Configuration.MaxParallelThreads);
			}

			[Theory]
			[InlineData("2x")]
			[InlineData("2.0x")]
			public static void MultiplierValue(string value)
			{
				var expected = Environment.ProcessorCount * 2;

				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads", value);

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Equal(expected, assembly.Configuration.MaxParallelThreads);
			}
		}

		public class Parallelization
		{
			[Fact]
			public static void ParallelizationOptionsAreNullByDefault()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Null(assembly.Configuration.ParallelizeTestCollections);
			}

			[Fact]
			public static void FailsWithoutOptionOrWithIncorrectOptions()
			{
				var commandLine1 = new TestableCommandLine("no-config.json", "-parallel");
				var exception1 = Record.Exception(() => commandLine1.Parse());
				Assert.IsType<ArgumentException>(exception1);
				Assert.Equal("missing argument for -parallel", exception1.Message);

				var commandLine2 = new TestableCommandLine("no-config.json", "-parallel", "nonsense");
				var exception2 = Record.Exception(() => commandLine2.Parse());
				Assert.IsType<ArgumentException>(exception2);
				Assert.Equal("incorrect argument value for -parallel", exception2.Message);
			}

			[Theory]
			[InlineData("none", false)]
			[InlineData("collections", true)]
			public static void ParallelCanBeTurnedOn(
				string parallelOption,
				bool expectedCollectionsParallelization)
			{
				var commandLine = new TestableCommandLine("no-config.json", "-parallel", parallelOption);

				var project = commandLine.Parse();

				foreach (var assembly in project.Assemblies)
					Assert.Equal(expectedCollectionsParallelization, assembly.Configuration.ParallelizeTestCollections);
			}
		}
	}

	public class Filters
	{
		[Fact]
		public static void DefaultFilters()
		{
			var commandLine = new TestableCommandLine("no-config.json");

			var project = commandLine.Parse();

			var filters = project.Assemblies.Single().Configuration.Filters;
			Assert.Equal(0, filters.IncludedTraits.Count);
			Assert.Equal(0, filters.ExcludedTraits.Count);
			Assert.Equal(0, filters.IncludedNamespaces.Count);
			Assert.Equal(0, filters.ExcludedNamespaces.Count);
			Assert.Equal(0, filters.IncludedClasses.Count);
			Assert.Equal(0, filters.ExcludedClasses.Count);
			Assert.Equal(0, filters.IncludedMethods.Count);
			Assert.Equal(0, filters.ExcludedMethods.Count);
		}

		static readonly (string Switch, Expression<Func<XunitProject, ICollection<string>>> Accessor)[] SwitchOptionsList =
			new (string, Expression<Func<XunitProject, ICollection<string>>>)[]
			{
				("-namespace", project => project.Assemblies.Single().Configuration.Filters.IncludedNamespaces),
				("-nonamespace", project => project.Assemblies.Single().Configuration.Filters.ExcludedNamespaces),
				("-class", project => project.Assemblies.Single().Configuration.Filters.IncludedClasses),
				("-noclass", project => project.Assemblies.Single().Configuration.Filters.ExcludedClasses),
				("-method", project => project.Assemblies.Single().Configuration.Filters.IncludedMethods),
				("-nomethod", project => project.Assemblies.Single().Configuration.Filters.ExcludedMethods),
			};

		public static readonly TheoryData<string, Expression<Func<XunitProject, ICollection<string>>>> SwitchesLowerCase =
			new(SwitchOptionsList);

		public static readonly TheoryData<string, Expression<Func<XunitProject, ICollection<string>>>> SwitchesUpperCase =
			new(SwitchOptionsList.Select(t => (t.Switch.ToUpperInvariant(), t.Accessor)));

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void MissingOptionValue(
			string @switch,
			Expression<Func<XunitProject, ICollection<string>>> _)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch);

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal($"missing argument for {@switch.ToLowerInvariant()}", exception.Message);
		}

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void SingleValidArgument(
			string @switch,
			Expression<Func<XunitProject, ICollection<string>>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch, "value1");
			var project = commandLine.Parse();

			var results = accessor.Compile().Invoke(project);

			var item = Assert.Single(results.OrderBy(x => x));
			Assert.Equal("value1", item);
		}

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void MultipleValidArguments(
			string @switch,
			Expression<Func<XunitProject, ICollection<string>>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch, "value2", @switch, "value1");
			var project = commandLine.Parse();

			var results = accessor.Compile().Invoke(project);

			Assert.Collection(results.OrderBy(x => x),
				item => Assert.Equal("value1", item),
				item => Assert.Equal("value2", item)
			);
		}

		public class Traits
		{
			static readonly (string Switch, Expression<Func<XunitProject, Dictionary<string, List<string>>>> Accessor)[] SwitchOptionsList =
				new (string Switch, Expression<Func<XunitProject, Dictionary<string, List<string>>>> Accessor)[]
				{
					("-trait", project => project.Assemblies.Single().Configuration.Filters.IncludedTraits),
					("-notrait", project => project.Assemblies.Single().Configuration.Filters.ExcludedTraits),
				};

			static readonly string[] BadFormatValues =
				new string[]
				{
					// Missing equals
					"foobar",
					// Missing value
					"foo=",
					// Missing name
					"=bar",
					// Double equal signs
					"foo=bar=baz",
				};

			public static readonly TheoryData<string, Expression<Func<XunitProject, Dictionary<string, List<string>>>>> SwitchesLowerCase =
				new(SwitchOptionsList);

			public static readonly TheoryData<string, Expression<Func<XunitProject, Dictionary<string, List<string>>>>> SwitchesUpperCase =
				new(SwitchOptionsList.Select(x => (x.Switch.ToUpperInvariant(), x.Accessor)));

			public static readonly TheoryData<string, string> SwitchesWithOptionsLowerCase =
				new(SwitchOptionsList.SelectMany(tuple => BadFormatValues.Select(value => (tuple.Switch, value))));

			public static readonly TheoryData<string, string> SwitchesWithOptionsUpperCase =
				new(SwitchOptionsList.SelectMany(tuple => BadFormatValues.Select(value => (tuple.Switch.ToUpperInvariant(), value))));

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void SingleValidTraitArgument(
				string @switch,
				Expression<Func<XunitProject, Dictionary<string, List<string>>>> accessor)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, "foo=bar");
				var project = commandLine.Parse();

				var traits = accessor.Compile().Invoke(project);

				Assert.Equal(1, traits.Count);
				Assert.Equal(1, traits["foo"].Count());
				Assert.Contains("bar", traits["foo"]);
			}

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void MultipleValidTraitArguments_SameName(
				string @switch,
				Expression<Func<XunitProject, Dictionary<string, List<string>>>> accessor)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, "foo=bar", @switch, "foo=baz");
				var project = commandLine.Parse();

				var traits = accessor.Compile().Invoke(project);

				Assert.Equal(1, traits.Count);
				Assert.Equal(2, traits["foo"].Count());
				Assert.Contains("bar", traits["foo"]);
				Assert.Contains("baz", traits["foo"]);
			}

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void MultipleValidTraitArguments_DifferentName(
				string @switch,
				Expression<Func<XunitProject, Dictionary<string, List<string>>>> accessor)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, "foo=bar", @switch, "baz=biff");
				var project = commandLine.Parse();

				var traits = accessor.Compile().Invoke(project);

				Assert.Equal(2, traits.Count);
				Assert.Equal(1, traits["foo"].Count());
				Assert.Contains("bar", traits["foo"]);
				Assert.Equal(1, traits["baz"].Count());
				Assert.Contains("biff", traits["baz"]);
			}

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void MissingOptionValue(
				string @switch,
				Expression<Func<XunitProject, Dictionary<string, List<string>>>> _)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch);

				var exception = Record.Exception(() => commandLine.Parse());

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal($"missing argument for {@switch.ToLowerInvariant()}", exception.Message);
			}

			[Theory]
			[MemberData(nameof(SwitchesWithOptionsLowerCase))]
			[MemberData(nameof(SwitchesWithOptionsUpperCase))]
			public static void ImproperlyFormattedOptionValue(
				string @switch,
				string optionValue)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, optionValue);

				var exception = Record.Exception(() => commandLine.Parse());

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal($"incorrect argument format for {@switch.ToLowerInvariant()} (should be \"name=value\")", exception.Message);
			}
		}
	}

	public class Transforms
	{
		public static readonly TheoryData<string> SwitchesLowerCase =
			new(TransformFactory.AvailableTransforms.Select(x => $"-{x.ID}"));

		public static readonly TheoryData<string> SwitchesUpperCase =
			new(TransformFactory.AvailableTransforms.Select(x => $"-{x.ID.ToUpperInvariant()}"));

		[Theory]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void OutputMissingFilename(string @switch)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch);

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal($"missing filename for {@switch}", exception.Message);
		}

		[Theory]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void Output(string @switch)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch, "outputFile");

			var project = commandLine.Parse();

			var output = Assert.Single(project.Configuration.Output);
			Assert.Equal(@switch.Substring(1).ToLowerInvariant(), output.Key);
			Assert.Equal("outputFile", output.Value);
		}
	}

	public class Reporters
	{
		[Fact]
		public void NoReporters_UsesDefaultReporter()
		{
			var commandLine = new TestableCommandLine("no-config.json");

			var project = commandLine.Parse();

			Assert.IsType<DefaultRunnerReporter>(project.RunnerReporter);
		}

		[Fact]
		public void NoExplicitReporter_NoEnvironmentallyEnabledReporters_UsesDefaultReporter()
		{
			var implicitReporter = Mocks.RunnerReporter(isEnvironmentallyEnabled: false);
			var commandLine = new TestableCommandLine(new[] { implicitReporter }, "no-config.json");

			var project = commandLine.Parse();

			Assert.IsType<DefaultRunnerReporter>(project.RunnerReporter);
		}

		[Fact]
		public void ExplicitReporter_NoEnvironmentalOverride_UsesExplicitReporter()
		{
			var explicitReporter = Mocks.RunnerReporter("switch");
			var commandLine = new TestableCommandLine(new[] { explicitReporter }, "no-config.json", "-switch");

			var project = commandLine.Parse();

			Assert.Same(explicitReporter, project.RunnerReporter);
		}

		[Fact]
		public void ExplicitReporter_WithEnvironmentalOverride_UsesEnvironmentalOverride()
		{
			var explicitReporter = Mocks.RunnerReporter("switch");
			var implicitReporter = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var commandLine = new TestableCommandLine(new[] { explicitReporter, implicitReporter }, "no-config.json", "-switch");

			var project = commandLine.Parse();

			Assert.Same(implicitReporter, project.RunnerReporter);
		}

		[Fact]
		public void WithEnvironmentalOverride_WithEnvironmentalOverridesDisabled_UsesDefaultReporter()
		{
			var implicitReporter = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var commandLine = new TestableCommandLine(new[] { implicitReporter }, "no-config.json", "-noautoreporters");

			var project = commandLine.Parse();

			Assert.IsType<DefaultRunnerReporter>(project.RunnerReporter);
		}

		[Fact]
		public void NoExplicitReporter_SelectsFirstEnvironmentallyEnabledReporter()
		{
			var explicitReporter = Mocks.RunnerReporter("switch");
			var implicitReporter1 = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var implicitReporter2 = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var commandLine = new TestableCommandLine(new[] { explicitReporter, implicitReporter1, implicitReporter2 }, "no-config.json");

			var project = commandLine.Parse();

			Assert.Same(implicitReporter1, project.RunnerReporter);
		}
	}

	class TestableCommandLine : CommandLine
	{
		public TestableCommandLine(params string[] arguments)
			: base(Assembly.GetExecutingAssembly(), arguments)
		{ }

		public TestableCommandLine(
			IReadOnlyList<IRunnerReporter> reporters,
			params string[] arguments)
				: base(Assembly.GetExecutingAssembly(), arguments, reporters)
		{ }

		protected override bool FileExists(string? path) =>
			path?.StartsWith("badConfig.") != true && path != "fileName";

		protected override string? GetFullPath(string? fileName) =>
			fileName == null ? null : $"/full/path/{fileName}";
	}
}
