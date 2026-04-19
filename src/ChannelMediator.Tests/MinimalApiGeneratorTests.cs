using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ChannelMediator.Tests;

public class MinimalApiGeneratorTests
{
	[Fact]
	public void WhenParameterlessRecordRequestReturnsValue_ThenExtractRecordParametersIgnoresSynthesizedCopyConstructor()
	{
		// Arrange
		const string source = """
using System.Collections.Generic;
using ChannelMediator;

namespace TestAssembly.Contracts;

public record Brand(string Name);
public record GetAllBrands : IRequest<List<Brand>>;
""";

		var compilation = CSharpCompilation.Create(
			assemblyName: "TestAssembly",
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			references: CreateReferences(),
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var typeSymbol = compilation.GetTypeByMetadataName("TestAssembly.Contracts.GetAllBrands");
		Assert.NotNull(typeSymbol);

		var method = typeof(ChannelMediator.MinimalApiGenerator.MinimalApiGenerator)
			.GetMethod("ExtractRecordParameters", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);

		// Act
		var parameters = (IEnumerable<object>)method.Invoke(null, [typeSymbol!])!;

		// Assert
		Assert.Empty(parameters);
	}

	private static IEnumerable<MetadataReference> CreateReferences()
	{
		var assemblies = new[]
		{
			typeof(object).Assembly,
			typeof(List<>).Assembly,
			typeof(ChannelMediator.IRequest<>).Assembly,
			Assembly.Load("netstandard")
		};

		return assemblies
			.Distinct()
			.Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
	}
}
