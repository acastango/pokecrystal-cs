namespace PokeCrystal.Scripting;

using Microsoft.Extensions.DependencyInjection;
using PokeCrystal.Scripting.Commands;
using PokeCrystal.Scripting.Specials;

public static class ScriptingRegistry
{
    public static IServiceCollection AddCrystalScripting(this IServiceCollection services)
    {
        services.AddSingleton<ScriptRegistry>();
        services.AddSingleton<SpecialRegistry>();

        services.AddSingleton<ScriptEngine>(sp =>
        {
            var registry = sp.GetRequiredService<ScriptRegistry>();
            var specials = sp.GetRequiredService<SpecialRegistry>();
            // Build base commands (no engine reference needed)
            var commands = BuildBaseCommands(specials);
            var engine = new ScriptEngine(commands, registry, specials);
            // Register engine-dependent commands after construction to avoid circular dependency
            engine.RegisterCommand(new EndCallbackCommand(engine));
            engine.RegisterCommand(new EndCommand(engine));
            engine.RegisterCommand(new EndAllCommand(engine));
            return engine;
        });

        return services;
    }

    private static Dictionary<byte, IScriptCommand> BuildBaseCommands(SpecialRegistry specials)
    {
        var cmds = new List<IScriptCommand>
        {
            // Control flow
            new ScallCommand(),
            new SjumpCommand(),
            new IfEqualCommand(),
            new IfNotEqualCommand(),
            new IfFalseCommand(),
            new IfTrueCommand(),
            new IfGreaterCommand(),
            new IfLessCommand(),
            new JumpStdCommand(),
            new CallStdCommand(),
            new WaitCommand(),
            new PauseCommand(),

            // Data
            new SetValCommand(),
            new AddValCommand(),
            new RandomCommand(),
            new CheckTimeCommand(),
            new CheckVerCommand(),

            // World
            new GiveItemCommand(),
            new TakeItemCommand(),
            new CheckItemCommand(),
            new GiveMoneyCommand(),
            new TakeMoneyCommand(),
            new CheckMoneyCommand(),
            new GiveCoinsCommand(),
            new TakeCoinsCommand(),
            new CheckCoinsCommand(),
            new CheckEventCommand(),
            new ClearEventCommand(),
            new SetEventCommand(),
            new CheckFlagCommand(),
            new ClearFlagCommand(),
            new SetFlagCommand(),
            new CheckSceneCommand(),
            new SetSceneCommand(),
            new CheckMapSceneCommand(),
            new SetMapSceneCommand(),
            new WarpCommand(),
            new AddCellNumCommand(),
            new DelCellNumCommand(),
            new CheckCellNumCommand(),

            // Pokemon
            new CheckPokeCommand(),
            new GivePokeCommand(),
            new GiveEggCommand(),

            // Battle
            new LoadWildMonCommand(),
            new LoadTrainerCommand(),
            new StartBattleCommand(),
            new ReloadMapAfterBattleCommand(),

            // NPC
            new ApplyMovementCommand(),
            new ApplyMovementLastTalkedCommand(),
            new FacePlayerCommand(),
            new AppearCommand(),
            new DisappearCommand(),

            // Text
            new OpenTextCommand(),
            new CloseTextCommand(),
            new WriteTextCommand(),
            new FarWriteTextCommand(),
            new YesOrNoCommand(),
            new CloseWindowCommand(),
            new WaitButtonCommand(),

            // Audio
            new PlayMusicCommand(),
            new CryCommand(),
            new PlaySoundCommand(),
            new WaitSfxCommand(),

            // Special
            new SpecialCommand(specials),
        };

        return cmds.ToDictionary(c => c.Opcode);
    }

}
