using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FixHaulCalculation;

[BepInPlugin( PluginGUID, PluginName, PluginVersion )]
public class Plugin : BaseUnityPlugin
{
    internal const string PluginGUID =  "ErrorJan.REPO.FixHaulCalculation";
    internal const string PluginName = "Fix Haul Calculation";
    internal const string PluginVersion = "1.0.0"; 
    internal static ManualLogSource _logger;

    private void Awake()
    {
        _logger = Logger;

        Harmony harmony = new( PluginGUID );
        harmony.PatchAll( typeof( HarmonyPatches ) );

        Logger.LogInfo($"Plugin { PluginName } is loaded!");
    }

    private class HarmonyPatches
    {
        public static int UpdateAndCalculateSurplus( ExtractionPoint instance )
        {
            var currentHaul = AccessTools.Field( typeof( RoundDirector ), "currentHaul" );
            var extractionPointSurplus = AccessTools.Field( typeof( RoundDirector ), "extractionPointSurplus" );

            int totalHaul = (int)currentHaul.GetValue( RoundDirector.instance ) + (int)extractionPointSurplus.GetValue( RoundDirector.instance );
            return totalHaul - instance.haulGoal;
        }

        [HarmonyTranspiler]
        [HarmonyPatch( typeof( ExtractionPoint ), "StateExtracting" )]
        private static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> instructions )
        {
            var extractionPointsCompleted = AccessTools.Field( typeof( RoundDirector ), "extractionPointsCompleted" );
            var extractionPoints = AccessTools.Field( typeof( RoundDirector ), "extractionPoints" );
            var haulSurplus = AccessTools.Field( typeof( ExtractionPoint ), "haulSurplus" );

            var method_updateAndCalculateSurplus = AccessTools.Method( typeof( HarmonyPatches ), "UpdateAndCalculateSurplus" );

            int check1 = 0;
            int check2 = 0;
            bool check3 = false;
            bool isPatched = false;

            foreach ( var instruction in instructions )
            {
                if ( !check3 )
                {
                    if ( instruction.opcode.FlowControl == FlowControl.Cond_Branch && check1 > 0 && check2 > 0 )
                        check3 = true;

                    check1--;
                    check2--;

                    if ( instruction.LoadsField( extractionPointsCompleted ) )
                        check1 = 6;
                    if ( instruction.LoadsField( extractionPoints ) )
                        check2 = 5;
                }
                else if ( !isPatched )
                {
                    if ( instruction.LoadsField( haulSurplus ) )
                    {
                        yield return new CodeInstruction( OpCodes.Call, method_updateAndCalculateSurplus );

                        isPatched = true;
                        continue;
                    }
                }
                
                yield return instruction;
            }
        }
    }
}
