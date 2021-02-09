using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using System.Threading;

// namespace HoloLab.WebRtcModule
namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class DynamicTransceiversPeerConnection : Microsoft.MixedReality.WebRTC.Unity.PeerConnection
    {
        //[SerializeField]
        //private MediaLine selfMedia;

        [SerializeField]
        private MediaTrackSource selfVideoTrackSource = null;

        [SerializeField]
        private MediaTrackSource selfAudioTrackSource = null;

        [SerializeField]
        private GameObject remoteMediaPrefab = null;

        /// <summary>
        /// 自分のビデオ送信がアクティブかどうか
        /// </summary>
        public bool LocalVideoEnabled => selfVideoTrackSource.gameObject.activeSelf;

        /// <summary>
        /// 自分のマイク送信がアクティブかどうか
        /// </summary>
        public bool LocalAudioEnabled => selfAudioTrackSource.gameObject.activeSelf;

        public event Action<string, GameObject> OnRemoteMediaCreated;

        private Dictionary<string, GameObject> remoteMediaDictionary = new Dictionary<string, GameObject>();

        protected override void Awake()
        {
            base.Awake();

            OnShutdown.AddListener(PeerConnection_OnShutdown);
        }

        private void PeerConnection_OnShutdown()
        {
            foreach (var media in remoteMediaDictionary.Values)
            {
                Destroy(media);
            }
            remoteMediaDictionary.Clear();

            foreach (var mediaLine in _mediaLines)
            {
                mediaLine.UnpairTransceiver();
                mediaLine.OnDestroy();
            }

            _mediaLines.Clear();
        }

        private CancellationTokenSource tokenSource;

        public async Task SetLocalVideoEnabled(bool enabled)
        {
            tokenSource?.Cancel();
            tokenSource?.Dispose();

            tokenSource = new CancellationTokenSource();
            await SetLocalVideoTrackSourceActive(enabled, tokenSource.Token);
        }

        private async Task SetLocalVideoTrackSourceActive(bool enabled, CancellationToken token)
        {
            if (!enabled)
            {
                // 相手に送信しているカメラ映像が黒い画面になるよう、
                // localVideoTrack をオフにして少し待ってから webcamSource を非アクティブにする

                for (var i = 0; i < 10; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    foreach (var localVideoTrack in Peer.LocalVideoTracks)
                    {
                        localVideoTrack.Enabled = false;
                    }
                    await Task.Delay(200);
                }
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (enabled)
            {
                // webcamSource が非アクティブになる前にカメラの有効化が呼ばれてしまったときのために、
                // 一度非アクティブにしてからアクティブにする
                // これを行わないとカメラ映像が送れなくなる
                selfVideoTrackSource.gameObject.SetActive(false);
                selfVideoTrackSource.gameObject.SetActive(true);
            }
            else
            {
                selfVideoTrackSource.gameObject.SetActive(false);
            }
        }

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
            transceivers.Sort((tr1, tr2) => (tr1.MlineIndex - tr2.MlineIndex));

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


                    mediaLine = AddMediaLine(tr.MediaKind);

                    var streamId = tr.StreamIDs.FirstOrDefault();

                    if (string.IsNullOrEmpty(streamId))
                    {
                        switch (tr.MediaKind)
                        {
                            case MediaKind.Video:
                                mediaLine.Source = selfVideoTrackSource;
                                break;
                            case MediaKind.Audio:
                                mediaLine.Source = selfAudioTrackSource;
                                break;
                        }
                    }
                    else
                    {
                        if (!remoteMediaDictionary.TryGetValue(streamId, out var remoteMedia))
                        {
                            remoteMedia = Instantiate(remoteMediaPrefab);
                            remoteMediaDictionary[streamId] = remoteMedia;
                            OnRemoteMediaCreated?.Invoke(streamId, remoteMedia);
                        }

                        switch (tr.MediaKind)
                        {
                            case MediaKind.Video:
                                var remoteVideoReceiver = remoteMedia.GetComponentInChildren<VideoReceiver>();
                                mediaLine.Receiver = remoteVideoReceiver;
                                break;
                            case MediaKind.Audio:
                                var remoteAudioReceiver = remoteMedia.GetComponentInChildren<AudioReceiver>();
                                mediaLine.Receiver = remoteAudioReceiver;
                                break;
                        }
                    }

                    mediaLine.PairTransceiver(tr);

                    try
                    {
                        mediaLine.UpdateAfterSdpReceived();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }


                    Debug.Log($"after: {tr.NegotiatedDirection}, {tr.DesiredDirection}, {Transceiver.HasRecv(desDir)}, {Transceiver.HasSend(desDir)}");

                }
            }
        }
    }
}

