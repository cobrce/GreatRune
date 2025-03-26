
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v1.1.0.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace GreatRune {
    using System.Diagnostics;
    using GreatRune.GameManagers;
    using Terminal.Gui;
    using static GreatRune.GameManagers.RunesHelper;
    using System.Diagnostics.CodeAnalysis;

    public partial class MainDialog {
        private string lblStatusOriginalText;
        private readonly object timerToken;

        private readonly MemoryManager memoryManager  = new();
        private readonly ProcessSelectorFSM processSelectorFSM;
        const string EldenRingProcessName = "eldenring";


        public MainDialog() {
            InitializeComponent();
            // hook = new(1000, 1000, 
            //     p => p.MainWindowTitle is "ELDEN RING™" 
            //          || (p.MainWindowTitle is "ELDEN RING" && p.ProcessName == "eldenring"));



            // hook.OnSetup+=hook_OnSetup;
            // hook.OnUnhooked+=hook_OnUnhooked;
            processSelectorFSM = new ProcessSelectorFSM(EldenRingProcessName,memoryManager);
            timerToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(200),TimerTick);

            btnQuit.Clicked += ()=>{
                Application.MainLoop.RemoveTimeout(timerToken);
                Application.RequestStop();
                };
            this.lblStatusOriginalText = lblStatus.Text.ToString();
            lblStatus.ColorScheme = null;

            chkGodrick.Enabled = false;
            chkMalenia.Enabled = false;
            chkMohg.Enabled = false;
            chkMorgott.Enabled = false;
            chkRadahn.Enabled = false;
            chkRadahn.Enabled =  false;
            chkRennala.Enabled = false;
            chkRykard.Enabled = false;

        }


#nullable enable
        // private void hook_OnUnhooked(object? sender, PHEventArgs e)
        // {
        //     GameProcess = null;
        // }

        // void hook_OnSetup(object? sender, PHEventArgs e)
        // {
        //     GameProcess = hook.Process;
        // }

        Process? _gameProcess;
        public Process? GameProcess
        {
            get { return _gameProcess; }
            private set
            {
                chkAuto.Enabled = (value != null);
                if (_gameProcess!=value)
                    chkAuto.Checked = false;
                
                _gameProcess = value;
                lblStatus.Text = (value != null) ? $" Process found, ID : {value.Id}" : lblStatusOriginalText;
                lblStatus.ColorScheme = (value != null) ? gold : null;
            }
        }


        private bool TimerTick(MainLoop mainLoop)
        {
            processSelectorFSM.SearchForProcess(out Process? process);
            GameProcess = process;
            if (!memoryManager.IsOpen)
                // ReadGreatRunes();

            // hook.Refresh();
            // if (GameProcess==null)
                return true;

            ReadGreatRunes(out GreatRunesRecord greatRunes, out GreatRunesRecord activatedRunes);
            UpdateInterface(greatRunes,activatedRunes);
            if (chkAuto.Checked)
                ActivateRunes(greatRunes,activatedRunes);
            return true;
        }

        private void ActivateRunes(GreatRunesRecord greatRunes, GreatRunesRecord activatedRunes)
        {
            if (greatRunes.Godrick && !activatedRunes.Godrick)
                SpawnItem((int)GreatRunesID.GODRICK_S_GREAT_RUNE | 0x4000_0000);

            if (greatRunes.Malenia && !activatedRunes.Malenia)
                SpawnItem((int)GreatRunesID.MALENIA_S_GREAT_RUNE | 0x4000_0000);

            if (greatRunes.Mohg && !activatedRunes.Mohg)
                SpawnItem((int)GreatRunesID.MOHG_S_GREAT_RUNE | 0x4000_0000);

            if (greatRunes.Morgott && !activatedRunes.Morgott)
                SpawnItem((int)GreatRunesID.MORGOTT_S_GREAT_RUNE | 0x4000_0000);

            if (greatRunes.Radahn && !activatedRunes.Radahn)
                SpawnItem((int)GreatRunesID.RADAHN_S_GREAT_RUNE | 0x4000_0000);

            if (greatRunes.Rykard && !activatedRunes.Rykard)
                SpawnItem((int)GreatRunesID.RYKARD_S_GREAT_RUNE | 0x4000_0000);
        }

        private void SpawnItem(int id)
        {
            memoryManager.ItemGib(id,1);
        }

        private void UpdateInterface(GreatRunesRecord greatRunes, GreatRunesRecord activatedRunes)
        {
            chkGodrick.Checked = greatRunes.Godrick | activatedRunes.Godrick;
            chkMalenia.Checked = greatRunes.Malenia | activatedRunes.Malenia;
            chkMohg.Checked = greatRunes.Mohg | activatedRunes.Mohg;
            chkMorgott.Checked = greatRunes.Morgott | activatedRunes.Morgott;
            chkRadahn.Checked = greatRunes.Radahn | activatedRunes.Radahn;
            chkRennala.Checked = greatRunes.Rennala | activatedRunes.Rennala;
            chkRykard.Checked = greatRunes.Rykard | activatedRunes.Rykard;

            chkGodrick.ColorScheme = activatedRunes.Godrick ? gold :null;
            chkMalenia.ColorScheme = activatedRunes.Malenia ? gold :null;
            chkMohg.ColorScheme = activatedRunes.Mohg ? gold :null;
            chkMorgott.ColorScheme = activatedRunes.Morgott ? gold :null;
            chkRadahn.ColorScheme = activatedRunes.Radahn ? gold :null;
            chkRennala.ColorScheme = activatedRunes.Rennala ? gold :null;
            chkRykard.ColorScheme = activatedRunes.Rykard ? gold :null;

        }

        private void ReadGreatRunes(out GreatRunesRecord greatRunes, out GreatRunesRecord activatedRunes)
        {
            if (!memoryManager.UpdateInventory())
            {
                greatRunes = new(false,false,false,false,false,false,false);
                activatedRunes = new(false,false,false,false,false,false,false);
                return;
            }

            // var inventory = hook.PlayerGameData.Inventory.GetKeyInventory();

            // uint[] inventoryItems = inventory.Select((entry) =>(uint)entry.ItemID).ToArray();;

            greatRunes = RunesHelper.GreatRunes(memoryManager.InventoryItems);
            activatedRunes = RunesHelper.ActivatedRunes(memoryManager.InventoryItems);

          
        }
    }
}
