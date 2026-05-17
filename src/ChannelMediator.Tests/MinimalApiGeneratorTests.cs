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

	[Fact]
	public void WhenIStreamRequest_ThenExtractResponseTypeInfoReturnsIsStream()
	{
		// Arrange
		const string source = """
using ChannelMediator;

namespace TestAssembly.Contracts;

public record OrderLine(int Id);
public record GetOrderLines(int OrderId) : IStreamRequest<OrderLine>;
""";

		var compilation = CSharpCompilation.Create(
			assemblyName: "TestAssembly",
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			references: CreateReferences(),
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var typeSymbol = compilation.GetTypeByMetadataName("TestAssembly.Contracts.GetOrderLines");
		Assert.NotNull(typeSymbol);

		var method = typeof(ChannelMediator.MinimalApiGenerator.MinimalApiGenerator)
			.GetMethod("ExtractResponseTypeInfo", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);

		// Act
		var result = method.Invoke(null, [typeSymbol!]);
		Assert.NotNull(result);

		var resultType = result.GetType();
		var isStream = (bool)resultType.GetField("Item3")!.GetValue(result)!;
		var isNullable = (bool)resultType.GetField("Item1")!.GetValue(result)!;
		var typeName = (string)resultType.GetField("Item2")!.GetValue(result)!;

		// Assert
		Assert.True(isStream);
		Assert.False(isNullable);
		Assert.Equal("TestAssembly.Contracts.OrderLine", typeName);
	}

	[Fact]
	public void WhenIRequest_ThenExtractResponseTypeInfoReturnsIsStreamFalse()
	{
		// Arrange
		const string source = """
using ChannelMediator;

namespace TestAssembly.Contracts;

public record Product(int Id);
public record GetProduct(int Id) : IRequest<Product>;
""";

		var compilation = CSharpCompilation.Create(
			assemblyName: "TestAssembly",
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			references: CreateReferences(),
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var typeSymbol = compilation.GetTypeByMetadataName("TestAssembly.Contracts.GetProduct");
		Assert.NotNull(typeSymbol);

		var method = typeof(ChannelMediator.MinimalApiGenerator.MinimalApiGenerator)
			.GetMethod("ExtractResponseTypeInfo", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);

		// Act
		var result = method.Invoke(null, [typeSymbol!])!;
		var resultType = result.GetType();
		var isStream = (bool)resultType.GetField("Item3")!.GetValue(result)!;

		// Assert
		Assert.False(isStream);
	}

	[Fact]
	public void WhenIStreamRequestWithParams_ThenExtractRecordParametersExtractsParams()
	{
		// Arrange
		const string source = """
using ChannelMediator;

namespace TestAssembly.Contracts;

public record OrderLine(int Id);
public record GetOrderLines(int OrderId, string Status) : IStreamRequest<OrderLine>;
""";

		var compilation = CSharpCompilation.Create(
			assemblyName: "TestAssembly",
			syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
			references: CreateReferences(),
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var typeSymbol = compilation.GetTypeByMetadataName("TestAssembly.Contracts.GetOrderLines");
		Assert.NotNull(typeSymbol);

		var method = typeof(ChannelMediator.MinimalApiGenerator.MinimalApiGenerator)
			.GetMethod("ExtractRecordParameters", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);

		// Act
		var parameters = ((IEnumerable<object>)method.Invoke(null, [typeSymbol!])!).ToList();

		// Assert: stream requests with params should have params extracted
		Assert.Equal(2, parameters.Count);
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
