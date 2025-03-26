using System.Diagnostics;

namespace GreatRune.GameManagers
{
    internal class ProcessSelectorFSM
    {
        public ProcessSelectorFSM(string processName, MemoryManager memoryManager)
        {
            ProcessName = processName;
            MemoryManager = memoryManager;
        }

        private enum SearchState
        {
            NotFound,
            LookingForAob,
            Found,
            ResetSearch,
        };

        public void ResetSearch()
        {
            MemoryManager.Close();
            process = null;
            searchState = SearchState.NotFound;
        }

        public bool Update(out Process? GameProcess)
        {
            bool result = false;
            switch (searchState)
            {
                case SearchState.NotFound:
                    this.process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                    if (process != null)
                        searchState = SearchState.LookingForAob;
                    break;

                case SearchState.LookingForAob:
                    if (process != null && MemoryManager.Open(process))
                    {
                        searchState = SearchState.Found;
                        result = true;
                    }
                    else
                    {
                        searchState = SearchState.ResetSearch;
                    }
                    break;

                case SearchState.Found:
                    if (process?.WaitForExit(10) == true)
                        searchState = SearchState.ResetSearch;
                    else
                        result = true;
                    break;

                case SearchState.ResetSearch:
                    ResetSearch();

                    break;
                default:
                    break;
            }
            GameProcess = MemoryManager.IsOpen ? process : null;
            return result;
        }

        private SearchState searchState = SearchState.NotFound;
        private Process? process;

        public string ProcessName { get; }
        public MemoryManager MemoryManager { get; }
    }
}
