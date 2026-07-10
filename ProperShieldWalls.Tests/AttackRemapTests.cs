using ProperShieldWalls;
using Xunit;

public class AttackRemapTests
{
    private const uint MoveForward = 0x1u;   // an unrelated bit, must be preserved

    [Fact]
    public void NoOp_WhenNotCrowded()
    {
        uint flags = AttackRemap.AttackLeft | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: false));
    }

    [Fact]
    public void NoOp_WhenWeaponCannotSwing()
    {
        uint flags = AttackRemap.AttackLeft | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: false, isCrowded: true));
    }

    [Fact]
    public void NoOp_WhenNotAttacking()
    {
        uint flags = MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: true));
    }

    [Fact]
    public void NoOp_WhenAlreadyOverhead()
    {
        uint flags = AttackRemap.AttackUp | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: true));
    }

    [Fact]
    public void NoOp_WhenThrusting()
    {
        uint flags = AttackRemap.AttackDown | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: true));
    }

    [Fact]
    public void RemapsLeftSwingToOverhead()
    {
        uint result = AttackRemap.Decide(AttackRemap.AttackLeft, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp, result);
    }

    [Fact]
    public void RemapsRightSwingToOverhead()
    {
        uint result = AttackRemap.Decide(AttackRemap.AttackRight, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp, result);
    }

    [Fact]
    public void PreservesNonAttackBits()
    {
        uint result = AttackRemap.Decide(AttackRemap.AttackRight | MoveForward, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp | MoveForward, result);
    }

    [Fact]
    public void ClearsAllOtherAttackBits()
    {
        // Left+Down set simultaneously must collapse to Up alone.
        uint result = AttackRemap.Decide(AttackRemap.AttackLeft | AttackRemap.AttackDown, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp, result);
    }

    [Fact]
    public void ConstantsMatchMovementControlFlag()
    {
        // Verified against Agent.MovementControlFlag in the v1.4.6 decompile.
        Assert.Equal(0x40u,  AttackRemap.AttackLeft);
        Assert.Equal(0x80u,  AttackRemap.AttackRight);
        Assert.Equal(0x100u, AttackRemap.AttackUp);
        Assert.Equal(0x200u, AttackRemap.AttackDown);
        Assert.Equal(0x3C0u, AttackRemap.AttackMask);
    }
}
