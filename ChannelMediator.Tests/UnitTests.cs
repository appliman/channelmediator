namespace ChannelMediator.Tests;

public class UnitTests
{
	[Fact]
	public void Value_ReturnsSingletonInstance()
	{
		// Arrange & Act
		var value1 = Unit.Value;
		var value2 = Unit.Value;

		// Assert
		value1.Should().Be(value2);
	}

	[Fact]
	public void Task_ReturnsSameCompletedTask()
	{
		// Arrange & Act
		var task1 = Unit.Task;
		var task2 = Unit.Task;

		// Assert
		task1.Should().BeSameAs(task2);
		task1.IsCompletedSuccessfully.Should().BeTrue();
		task1.Result.Should().Be(Unit.Value);
	}

	[Fact]
	public void ValueTask_ReturnsCompletedValueTask()
	{
		// Arrange & Act
		var valueTask1 = Unit.ValueTask;
		var valueTask2 = Unit.ValueTask;

		// Assert
		valueTask1.IsCompletedSuccessfully.Should().BeTrue();
		valueTask1.Result.Should().Be(Unit.Value);
	}

	[Fact]
	public void Equals_WithUnit_ReturnsTrue()
	{
		// Arrange
		var unit1 = Unit.Value;
		var unit2 = new Unit();

		// Act & Assert
		unit1.Equals(unit2).Should().BeTrue();
		unit1.Should().Be(unit2);
	}

	[Fact]
	public void Equals_WithObject_ReturnsCorrectly()
	{
		// Arrange
		var unit = Unit.Value;

		// Act & Assert
		unit.Equals((object)new Unit()).Should().BeTrue();
		unit.Equals((object?)"test").Should().BeFalse();
		unit.Equals((object?)null).Should().BeFalse();
	}

	[Fact]
	public void EqualityOperator_ReturnsTrue()
	{
		// Arrange
		var unit1 = Unit.Value;
		var unit2 = new Unit();

		// Act & Assert
		(unit1 == unit2).Should().BeTrue();
	}

	[Fact]
	public void InequalityOperator_ReturnsFalse()
	{
		// Arrange
		var unit1 = Unit.Value;
		var unit2 = new Unit();

		// Act & Assert
		(unit1 != unit2).Should().BeFalse();
	}

	[Fact]
	public void GetHashCode_ReturnsZero()
	{
		// Arrange
		var unit = Unit.Value;

		// Act
		var hashCode = unit.GetHashCode();

		// Assert
		hashCode.Should().Be(0);
	}

	[Fact]
	public void CompareTo_WithUnit_ReturnsZero()
	{
		// Arrange
		var unit1 = Unit.Value;
		var unit2 = new Unit();

		// Act
		var result = unit1.CompareTo(unit2);

		// Assert
		result.Should().Be(0);
	}

	[Fact]
	public void CompareTo_WithObject_ReturnsZero()
	{
		// Arrange
		var unit = Unit.Value;
		IComparable comparable = unit;

		// Act
		var result = comparable.CompareTo(new Unit());

		// Assert
		result.Should().Be(0);
	}

	[Fact]
	public void CompareTo_WithNull_ReturnsZero()
	{
		// Arrange
		var unit = Unit.Value;
		IComparable comparable = unit;

		// Act
		var result = comparable.CompareTo(null);

		// Assert
		result.Should().Be(0);
	}

	[Fact]
	public void ToString_ReturnsEmptyParentheses()
	{
		// Arrange
		var unit = Unit.Value;

		// Act
		var result = unit.ToString();

		// Assert
		result.Should().Be("()");
	}

	[Fact]
	public void MultipleInstances_AreEqual()
	{
		// Arrange & Act
		var units = Enumerable.Range(0, 10).Select(_ => new Unit()).ToList();

		// Assert
		units.Should().AllSatisfy(u => u.Should().Be(Unit.Value));
		units.Distinct().Should().ContainSingle();
	}
}
