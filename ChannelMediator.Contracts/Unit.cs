namespace ChannelMediator.Contracts;

/// <summary>
/// Represents a void response type for requests that don't return a value.
/// </summary>
public struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
	private static readonly Unit _value = new();

	/// <summary>
	/// Gets the singleton instance of Unit.
	/// </summary>
	public static ref readonly Unit Value => ref _value;

	/// <summary>
	/// Returns the Task representing the Unit value.
	/// </summary>
	public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(_value);

	/// <summary>
	/// Returns the ValueTask representing the Unit value.
	/// </summary>
	public static ValueTask<Unit> ValueTask { get; } = new(_value);

	public int CompareTo(Unit other) => 0;

	int IComparable.CompareTo(object? obj) => 0;

	public override int GetHashCode() => 0;

	public bool Equals(Unit other) => true;

	public override bool Equals(object? obj) => obj is Unit;

	public static bool operator ==(Unit left, Unit right) => true;

	public static bool operator !=(Unit left, Unit right) => false;

	public override string ToString() => "()";
}
