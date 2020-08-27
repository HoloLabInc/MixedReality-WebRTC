using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;

// namespace HoloLab.WebRtcModule
namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class DynamicTransceiversPeerConnection : Microsoft.MixedReality.WebRTC.Unity.PeerConnection
    {
        //[SerializeField]
        //private MediaLine selfMedia;

        [SerializeField]
        private MediaTrackSource selfVideoTrackSource;

        [SerializeField]
        private GameObject remoteMediaPrefab;


        public new async Task HandleConnectionMessageAsync(Microsoft.MixedReality.WebRTC.SdpMessage message)
        {
            // MediaLine manipulates some MonoBehaviour objects when managing senders and receivers
            EnsureIsMainAppThread();

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("Message received by disabled PeerConnection");
                return;
            }

            // First apply the remote description
            try
            {
                await Peer.SetRemoteDescriptionAsync(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Cannot apply remote description: {ex.Message}");
            }

            // Sort associated transceiver by media line index. The media line index is not the index of
            // the transceiver, but they are both monotonically increasing, so sorting by one or the other
            // yields the same ordered collection, which allows pairing transceivers and media lines.
            // TODO - Ensure PeerConnection.Transceivers is already sorted
            var transceivers = new List<Transceiver>(Peer.AssociatedTransceivers);
            //transceivers.Sort((tr1, tr2) => (tr1.MlineIndex - tr2.MlineIndex));

            Debug.Log("transceivers");

            /*
            foreach (var transceiver in transceivers)
            {
                Debug.Log(transceiver.Name);
                Debug.Log(transceiver.MlineIndex);
                foreach(var id in transceiver.StreamIDs)
                {
                    Debug.Log(id);
                }
            }
            */

            transceivers.Sort((tr1, tr2) => (tr1.MlineIndex - tr2.MlineIndex));
            /*
            transceivers.Sort((tr1, tr2) => (tr1.MlineIndex - tr2.MlineIndex));
            int numAssociatedTransceivers = transceivers.Count;
            int numMatching = Math.Min(numAssociatedTransceivers, _mediaLines.Count);
            */
            if (message.Type == SdpMessageType.Offer)
            {
                // Match transceivers with media line, in order
                for (int i = 0; i < transceivers.Count; ++i)
                {
                    var tr = transceivers[i];
                    var desDir = tr.DesiredDirection;
                    /*
                    Debug.Log(desDir);
                    Debug.Log(tr.NegotiatedDirection);
                    Debug.Log(tr.MediaKind);
                    */

                    Debug.Log($"mecount: {_mediaLines.Count}, {i}");

                    foreach (var id in tr.StreamIDs)
                    {
                        Debug.Log(id);
                    }

                    Debug.Log($"before: {tr.NegotiatedDirection}, {tr.DesiredDirection}, {Transceiver.HasRecv(desDir)}, {Transceiver.HasSend(desDir)}");



                    // 既存の Media Line を検索
                    var mediaLine = _mediaLines.FirstOrDefault(x => x.Transceiver == tr);

                    if (mediaLine != null)
                    {
                        Debug.Log("medialine found");
                        try
                        {
                            mediaLine.UpdateAfterSdpReceived();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex);
                            // LogErrorOnMediaLineException(ex, mediaLine, tr);
                        }
                        continue;
                    }


                    // MediaLine mediaLine;

                    
                    /*
                    if (_mediaLines.Count > i)
                    {
                        mediaLine = _mediaLines[i];

                        if (tr.DesiredDirection == Transceiver.Direction.Inactive)
                        {
                            if (tr.MediaKind == MediaKind.Video)
                            {
                                mediaLine.Source = selfVideoTrackSource;
                                Debug.Log("set source");
                            }
                        }

                        if (mediaLine.Transceiver == null)
                        {
                            mediaLine.PairTransceiver(tr);
                        }
                    }
                    else
                    {
                        mediaLine = AddMediaLine(tr.MediaKind);

                        if (tr.DesiredDirection == Transceiver.Direction.Inactive)
                        {
                            if (tr.MediaKind == MediaKind.Video)
                            {
                                mediaLine.Source = selfVideoTrackSource;
                                Debug.Log("set source");
                            }
                        }

                        mediaLine.PairTransceiver(tr);
                    }
                    */

                    mediaLine = AddMediaLine(tr.MediaKind);

                    // テスト中
                    // if (tr.DesiredDirection == Transceiver.Direction.Inactive)
                    if (string.IsNullOrEmpty(tr.StreamIDs.FirstOrDefault()))
                    {
                        if (tr.MediaKind == MediaKind.Video)
                        {
                            mediaLine.Source = selfVideoTrackSource;
                            Debug.Log("set source");
                        }
                    }
                    else
                    {
                        if (tr.MediaKind == MediaKind.Video)
                        {
                            var remoteMedia = Instantiate(remoteMediaPrefab);
                            var remoteVideoReceiver = remoteMedia.GetComponentInChildren<VideoReceiver>();
                            mediaLine.Receiver = remoteVideoReceiver;
                            Debug.Log("set receiver");
                        }

                    }

                    mediaLine.PairTransceiver(tr);

                    try
                        {
                            mediaLine.UpdateAfterSdpReceived();
                        }
                        catch (Exception ex)
                        {
                            //LogErrorOnMediaLineException(ex, mediaLine, tr);
                        }


                    Debug.Log($"after: {tr.NegotiatedDirection}, {tr.DesiredDirection}, {Transceiver.HasRecv(desDir)}, {Transceiver.HasSend(desDir)}");
                    /*
                    Debug.Log($"nedir: {tr.NegotiatedDirection}");
                    Debug.Log($"desdir: {tr.DesiredDirection}");

                    //Peer.Add
                    /// var mediaLine = new MediaLine(Peer, MediaKind.Audio);

                    Debug.Log(Transceiver.HasRecv(desDir));
                    Debug.Log(Transceiver.HasSend(desDir));
                    */

                    /*
                    var mediaLine = _mediaLines[i];
                    if (mediaLine.Transceiver == null)
                    {
                        mediaLine.PairTransceiver(tr);
                    }
                    else
                    {
                        Debug.Assert(tr == mediaLine.Transceiver);
                    }
                    */

                    // Associate the transceiver with the media line, if not already done, and associate
                    // the track components of the media line to the tracks of the transceiver.

                    /*
                    try
                    {
                        mediaLine.UpdateAfterSdpReceived();
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnMediaLineException(ex, mediaLine, tr);
                    }

                    // Check if the remote peer was planning to send something to this peer, but cannot.
                    bool wantsRecv = (mediaLine.Receiver != null);
                    if (!wantsRecv)
                    {
                        var desDir = tr.DesiredDirection;
                        if (Transceiver.HasRecv(desDir))
                        {
                            string peerName = name;
                            int idx = i;
                            InvokeOnAppThread(() => LogWarningOnMissingReceiver(peerName, idx));
                        }
                    }
                    */
                }
            }


            /*
            foreach (var transceiver in Peer.Transceivers)
            {
                if (transceiver.Name == "")
                {
                    Debug.Log("empty");
                }
                Debug.Log(transceiver.Name);
                Debug.Log(transceiver.MlineIndex);
                foreach (var id in transceiver.StreamIDs)
                {
                    Debug.Log(id);
                }
            }
            */
            /*
        // Once applied, try to pair transceivers and remote tracks with the Unity receiver components
        if (message.Type == SdpMessageType.Offer)
        {
            // Match transceivers with media line, in order
            for (int i = 0; i < numMatching; ++i)
            {
                var tr = transceivers[i];
                var mediaLine = _mediaLines[i];
                if (mediaLine.Transceiver == null)
                {
                    mediaLine.PairTransceiver(tr);
                }
                else
                {
                    Debug.Assert(tr == mediaLine.Transceiver);
                }

                // Associate the transceiver with the media line, if not already done, and associate
                // the track components of the media line to the tracks of the transceiver.
                try
                {
                    mediaLine.UpdateAfterSdpReceived();
                }
                catch (Exception ex)
                {
                    LogErrorOnMediaLineException(ex, mediaLine, tr);
                }

                // Check if the remote peer was planning to send something to this peer, but cannot.
                bool wantsRecv = (mediaLine.Receiver != null);
                if (!wantsRecv)
                {
                    var desDir = tr.DesiredDirection;
                    if (Transceiver.HasRecv(desDir))
                    {
                        string peerName = name;
                        int idx = i;
                        InvokeOnAppThread(() => LogWarningOnMissingReceiver(peerName, idx));
                    }
                }
            }

            // Ignore extra transceivers without a registered component to attach
            if (numMatching < numAssociatedTransceivers)
            {
                string peerName = name;
                InvokeOnAppThread(() =>
                {
                    for (int i = numMatching; i < numAssociatedTransceivers; ++i)
                    {
                        LogWarningOnIgnoredTransceiver(peerName, i);
                    }
                });
            }
        }
        else if (message.Type == SdpMessageType.Answer)
        {
            // Associate registered media senders/receivers with existing transceivers
            for (int i = 0; i < numMatching; ++i)
            {
                Transceiver tr = transceivers[i];
                var mediaLine = _mediaLines[i];
                Debug.Assert(mediaLine.Transceiver == transceivers[i]);
                mediaLine.UpdateAfterSdpReceived();
            }

            // Ignore extra transceivers without a registered component to attach
            if (numMatching < numAssociatedTransceivers)
            {
                string peerName = name;
                InvokeOnAppThread(() =>
                {
                    for (int i = numMatching; i < numAssociatedTransceivers; ++i)
                    {
                        LogWarningOnIgnoredTransceiver(peerName, i);
                    }
                });
            }
        }
        */
        }
    }
}
