using EAP.HC.Core.Entities.MES;
using System.Collections.Generic;

namespace EAP.HC.Core.Entities
{
    /// <summary>
    /// Runtime state for one configured source/destination LP pair.
    /// The state is deliberately separate from the normal CJ queue so a
    /// destination LP can be tracked in MES without creating a PJ.
    /// </summary>
    public class TransferJobState
    {
        public string EquipmentId { get; set; }
        public int SourcePortId { get; set; }
        public int DestinationPortId { get; set; }

        public string SourceCarrierId { get; set; }
        public string DestinationCarrierId { get; set; }
        public int SourceCarrierGeneration { get; set; }
        public int DestinationCarrierGeneration { get; set; }
        public string SourceBatchId { get; set; }
        public string DestinationBatchId { get; set; }

        public MESLotInfoDownloadReply SourceBatchInfo { get; set; }
        public MESLotInfoDownloadReply DestinationBatchInfo { get; set; }
        public List<int> SourceSlotMap { get; set; }
        public List<int> DestinationSlotMap { get; set; }

        public bool SourceTrackIn { get; set; }
        public bool DestinationTrackIn { get; set; }
        public bool SourceMapVerified { get; set; }
        public bool DestinationMapVerified { get; set; }
        public bool SourceTrackInInProgress { get; set; }
        public bool DestinationTrackInInProgress { get; set; }
        public bool SourcePjCreated { get; set; }
        public bool SourcePjCreateInProgress { get; set; }
        public bool SourcePjQueued { get; set; }
        public HashSet<string> SourceQueuedPjIds { get; } = new(System.StringComparer.OrdinalIgnoreCase);
        public bool DestinationSimWaferStatusSet { get; set; }
        public bool CjCreateInProgress { get; set; }
        public bool CjCreated { get; set; }
        public bool TrackOutInProgress { get; set; }
        public bool TrackOutCompleted { get; set; }
        public bool Failed { get; set; }
    }
}
