using ProperShieldWalls.Patches;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    /// <summary>
    /// Rewrites a crowded AI agent's horizontal swing into an overhead.
    ///
    /// This is the engine's sanctioned hook: Agent.OnAIInputSet does nothing but fan out to
    /// every AgentComponent, passing the pending flags by ref. Harmony-patching Agent.OnAIInputSet
    /// directly is NOT an option — it carries [MBCallback] (a native engine callback), and patching
    /// it folds every character into a spike even when the patch body is inert.
    /// </summary>
    internal sealed class AttackGateComponent : AgentComponent
    {
        internal AttackGateComponent(Agent agent) : base(agent) { }

        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            uint flags = (uint)movementFlag;
            AttackGate.ApplyToInput(Agent, (uint)eventFlag, ref flags);
            movementFlag = (Agent.MovementControlFlag)flags;
        }
    }
}
