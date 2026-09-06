using System.Collections.Generic;
using Verse;

namespace SoilRelocation.Removal.RuntimeTests
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class RemovalProbeMapComponent : MapComponent
    {
        internal const int CurrentSchemaVersion = 1;

        internal const string PhasePendingProfileCompletion = "PendingProfileCompletion";
        internal const string PhasePendingActiveJobCapture = "PendingActiveJobCapture";
        internal const string PhasePrepared = "Prepared";
        internal const string PhaseFirstLoadVerified = "FirstLoadVerified";
        internal const string PhaseComplete = "Complete";

        private int _schemaVersion = CurrentSchemaVersion;
        private string _scenario;
        private string _phase;
        private int _mapParentId = -1;
        private int _fixtureCellX = int.MinValue;
        private int _fixtureCellZ = int.MinValue;
        private string _originalTerrainDefName;
        private string _expectedTerrainDefName;
        private string _fixtureThingDefName;
        private string _expectedActiveJobDefName;
        private int _priorityPawnId = -1;
        private int _activeJobPawnId = -1;
        private List<string> _workTypeDefNames = new List<string>();
        private List<int> _workPriorities = new List<int>();
        private int _observedLoadCount;
        private int _firstVerificationLoadCount = -1;

        public RemovalProbeMapComponent(Map map) : base(map)
        {
        }

        internal Map OwnerMap => map;

        internal int SchemaVersion => _schemaVersion;

        internal string Scenario => _scenario;

        internal string Phase => _phase;

        internal int MapParentId => _mapParentId;

        internal bool HasScenario => !string.IsNullOrEmpty(_scenario);

        internal bool HasFixtureCell => _fixtureCellX != int.MinValue && _fixtureCellZ != int.MinValue;

        internal IntVec3 FixtureCell => HasFixtureCell ? new IntVec3(_fixtureCellX, 0, _fixtureCellZ) : IntVec3.Invalid;

        internal string OriginalTerrainDefName => _originalTerrainDefName;

        internal string ExpectedTerrainDefName => _expectedTerrainDefName;

        internal string FixtureThingDefName => _fixtureThingDefName;

        internal string ExpectedActiveJobDefName => _expectedActiveJobDefName;

        internal int PriorityPawnId => _priorityPawnId;

        internal int ActiveJobPawnId => _activeJobPawnId;

        internal IReadOnlyList<string> WorkTypeDefNames => _workTypeDefNames;

        internal IReadOnlyList<int> WorkPriorities => _workPriorities;

        internal int ObservedLoadCount => _observedLoadCount;

        internal int FirstVerificationLoadCount => _firstVerificationLoadCount;

        internal bool HasValidWorkPrioritySnapshot =>
            _workTypeDefNames != null && _workPriorities != null && _workTypeDefNames.Count > 0 &&
            _workTypeDefNames.Count == _workPriorities.Count;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _schemaVersion, "schemaVersion", CurrentSchemaVersion);
            Scribe_Values.Look(ref _scenario, "scenario");
            Scribe_Values.Look(ref _phase, "phase");
            Scribe_Values.Look(ref _mapParentId, "mapParentId", -1);
            Scribe_Values.Look(ref _fixtureCellX, "fixtureCellX", int.MinValue);
            Scribe_Values.Look(ref _fixtureCellZ, "fixtureCellZ", int.MinValue);
            Scribe_Values.Look(ref _originalTerrainDefName, "originalTerrainDefName");
            Scribe_Values.Look(ref _expectedTerrainDefName, "expectedTerrainDefName");
            Scribe_Values.Look(ref _fixtureThingDefName, "fixtureThingDefName");
            Scribe_Values.Look(ref _expectedActiveJobDefName, "expectedActiveJobDefName");
            Scribe_Values.Look(ref _priorityPawnId, "priorityPawnId", -1);
            Scribe_Values.Look(ref _activeJobPawnId, "activeJobPawnId", -1);
            Scribe_Collections.Look(ref _workTypeDefNames, "workTypeDefNames", LookMode.Value);
            Scribe_Collections.Look(ref _workPriorities, "workPriorities", LookMode.Value);
            Scribe_Values.Look(ref _observedLoadCount, "observedLoadCount");
            Scribe_Values.Look(ref _firstVerificationLoadCount, "firstVerificationLoadCount", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_workTypeDefNames == null)
                {
                    _workTypeDefNames = new List<string>();
                }

                if (_workPriorities == null)
                {
                    _workPriorities = new List<int>();
                }

                _observedLoadCount++;
            }
        }

        internal void BeginScenario(
            string scenario,
            string phase,
            IntVec3 fixtureCell,
            string originalTerrainDefName,
            string expectedTerrainDefName,
            string fixtureThingDefName,
            string expectedActiveJobDefName,
            int priorityPawnId,
            IReadOnlyList<string> workTypeDefNames,
            IReadOnlyList<int> workPriorities)
        {
            _schemaVersion = CurrentSchemaVersion;
            _scenario = scenario;
            _phase = phase;
            _mapParentId = map.Parent?.ID ?? -1;
            _fixtureCellX = fixtureCell.IsValid ? fixtureCell.x : int.MinValue;
            _fixtureCellZ = fixtureCell.IsValid ? fixtureCell.z : int.MinValue;
            _originalTerrainDefName = originalTerrainDefName;
            _expectedTerrainDefName = expectedTerrainDefName;
            _fixtureThingDefName = fixtureThingDefName;
            _expectedActiveJobDefName = expectedActiveJobDefName;
            _priorityPawnId = priorityPawnId;
            _activeJobPawnId = -1;
            _workTypeDefNames = workTypeDefNames == null ? new List<string>() : new List<string>(workTypeDefNames);
            _workPriorities = workPriorities == null ? new List<int>() : new List<int>(workPriorities);
            _firstVerificationLoadCount = -1;
        }

        internal void SetExpectedTerrain(string expectedTerrainDefName)
        {
            _expectedTerrainDefName = expectedTerrainDefName;
        }

        internal void MarkPrepared(int activeJobPawnId = -1)
        {
            _activeJobPawnId = activeJobPawnId;
            _phase = PhasePrepared;
        }

        internal void MarkFirstLoadVerified()
        {
            _firstVerificationLoadCount = _observedLoadCount;
            _phase = PhaseFirstLoadVerified;
        }

        internal void MarkComplete()
        {
            _phase = PhaseComplete;
        }
    }
}