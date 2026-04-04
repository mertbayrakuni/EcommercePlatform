using OrderService.Models;
using OrderService.Services;

namespace OrderService.Tests;

public class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void CanTransition_ValidTransition_ReturnsTrue(OrderStatus from, OrderStatus to)
        => Assert.True(OrderStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    [InlineData(OrderStatus.Paid, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Paid, OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Paid)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public void CanTransition_InvalidTransition_ReturnsFalse(OrderStatus from, OrderStatus to)
        => Assert.False(OrderStateMachine.CanTransition(from, to));

    [Fact]
    public void AllowedTargets_Pending_ReturnsPaidAndCancelled()
    {
        var result = OrderStateMachine.AllowedTargets(OrderStatus.Pending);
        Assert.Equal(new[] { OrderStatus.Paid, OrderStatus.Cancelled }, result);
    }

    [Fact]
    public void AllowedTargets_Paid_ReturnsShippedAndCancelled()
    {
        var result = OrderStateMachine.AllowedTargets(OrderStatus.Paid);
        Assert.Equal(new[] { OrderStatus.Shipped, OrderStatus.Cancelled }, result);
    }

    [Fact]
    public void AllowedTargets_Shipped_ReturnsDelivered()
    {
        var result = OrderStateMachine.AllowedTargets(OrderStatus.Shipped);
        Assert.Equal(new[] { OrderStatus.Delivered }, result);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void AllowedTargets_TerminalState_ReturnsEmpty(OrderStatus terminal)
        => Assert.Empty(OrderStateMachine.AllowedTargets(terminal));

    [Theory]
    [InlineData(OrderStatus.Pending, "Paid,Cancelled")]
    [InlineData(OrderStatus.Paid, "Shipped,Cancelled")]
    [InlineData(OrderStatus.Shipped, "Delivered")]
    [InlineData(OrderStatus.Delivered, "none")]
    [InlineData(OrderStatus.Cancelled, "none")]
    public void AllowedTargetsText_ReturnsExpectedString(OrderStatus from, string expected)
        => Assert.Equal(expected, OrderStateMachine.AllowedTargetsText(from));
}
