using Microsoft.CodeAnalysis;

namespace ChannelMediator.Generators.Shared;

/// <summary>
/// Shared Roslyn helpers reused across ChannelMediator source generators.
/// This file is linked (not copied) into each generator project.
/// </summary>
internal static class RoslynHelpers
{
	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="constructor"/> is the compiler-synthesised
	/// record copy constructor (<c>protected T(T original)</c>).
	/// Roslyn does not mark this constructor as <see cref="IMethodSymbol.IsImplicitlyDeclared"/>
	/// when the type is loaded from metadata, so callers must filter it explicitly.
	/// </summary>
	internal static bool IsRecordCopyConstructor(IMethodSymbol constructor, INamedTypeSymbol containingType)
		=> constructor.Parameters.Length == 1
		   && SymbolEqualityComparer.Default.Equals(
			   constructor.Parameters[0].Type.OriginalDefinition,
			   containingType.OriginalDefinition);
}
