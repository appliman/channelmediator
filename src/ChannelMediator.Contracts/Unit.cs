namespace ChannelMediator;

/// <summary>
/// Represents a void response type for requests that don't return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
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

	/// <summary>
	/// Compares the current instance with another <see cref="Unit"/> value.
	/// </summary>
	/// <param name="other">The other value to compare.</param>
	/// <returns>Always returns <c>0</c> because all <see cref="Unit"/> values are equivalent.</returns>
	public int CompareTo(Unit other) => 0;

	int IComparable.CompareTo(object? obj) => 0;

	/// <summary>
	/// Returns the hash code for this instance.
	/// </summary>
	/// <returns>Always returns <c>0</c> because all <see cref="Unit"/> values are equivalent.</returns>
	public override int GetHashCode() => 0;

	/// <summary>
	/// Determines whether the current instance is equal to another <see cref="Unit"/> value.
	/// </summary>
	/// <param name="other">The value to compare with the current instance.</param>
	/// <returns>Always returns <c>true</c> because all <see cref="Unit"/> values are equivalent.</returns>
	public bool Equals(Unit other) => true;

	/// <summary>
	/// Determines whether the current instance is equal to the specified object.
	/// </summary>
	/// <param name="obj">The object to compare with the current instance.</param>
	/// <returns><c>true</c> when <paramref name="obj"/> is a <see cref="Unit"/> value; otherwise, <c>false</c>.</returns>
	public override bool Equals(object? obj) => obj is Unit;

	/// <summary>
	/// Determines whether two <see cref="Unit"/> values are equal.
	/// </summary>
	/// <param name="left">The first value to compare.</param>
	/// <param name="right">The second value to compare.</param>
	/// <returns>Always returns <c>true</c> because all <see cref="Unit"/> values are equivalent.</returns>
	public static bool operator ==(Unit left, Unit right) => true;

	/// <summary>
	/// Determines whether two <see cref="Unit"/> values are not equal.
	/// </summary>
	/// <param name="left">The first value to compare.</param>
	/// <param name="right">The second value to compare.</param>
	/// <returns>Always returns <c>false</c> because all <see cref="Unit"/> values are equivalent.</returns>
	public static bool operator !=(Unit left, Unit right) => false;

	/// <summary>
	/// Returns the string representation of the value.
	/// </summary>
	/// <returns>The string literal <c>()</c>.</returns>
	public override string ToString() => "()";
}
