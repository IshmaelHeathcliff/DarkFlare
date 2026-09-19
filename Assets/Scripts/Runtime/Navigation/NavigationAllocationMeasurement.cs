using System;
using Unity.Profiling;

namespace DarkFlare
{
    public sealed partial class NavigationMap
    {
        ProfilerRecorder _allocationRecorder;
        bool _measureAllocations;
        public long AllocationProbeEvents { get; private set; }
        public void EnableAllocationMeasurement()
        {
            DisableAllocationMeasurement();
            _allocationRecorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!_allocationRecorder.Valid) { AllocationProbeEvents = -1; return; }
            _allocationRecorder.Start();
            var probe = new byte[512];
            _allocationRecorder.Stop();
            GC.KeepAlive(probe);
            AllocationProbeEvents = _allocationRecorder.Count > 0 ? _allocationRecorder.GetSample(0).Count : 0;
            _measureAllocations = true;
        }
        public void DisableAllocationMeasurement()
        {
            if (_allocationRecorder.Valid) { _allocationRecorder.Dispose(); }
            _allocationRecorder = default; _measureAllocations = false;
        }
        void BeginAllocationMeasurement()
        {
            if (_measureAllocations) { _allocationRecorder.Reset(); _allocationRecorder.Start(); }
        }
        void EndAllocationMeasurement()
        {
            if (!_measureAllocations) { return; }
            _allocationRecorder.Stop();
            _model.AllocationEvents = _allocationRecorder.Count > 0 ? _allocationRecorder.GetSample(0).Count : 0;
        }
    }
}
