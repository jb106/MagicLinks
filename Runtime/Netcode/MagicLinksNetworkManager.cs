using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicLinks.Network
{
    /// <summary>
    /// Starts and stops the synchronization of networked magic links with the NetworkManager.
    /// Put it on the NetworkManager GameObject. If it is missing, it is added automatically to
    /// NetworkManager.Singleton when a scene loads and at least one link is networked.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MagicLinks/Magic Links Network Manager")]
    public class MagicLinksNetworkManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;

        /// <summary>
        /// Called on the server for every write or event coming from a client: (clientId, link name).
        /// Return false to reject it. Async requests then complete with MagicWriteResult.Refused.
        /// </summary>
        public Func<ulong, string, bool> GlobalWriteRule;

        private MagicLinksNetworkSession _session;

        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (networkManager == null) networkManager = NetworkManager.Singleton;
        }

        private void OnEnable()
        {
            if (networkManager == null)
            {
                Debug.LogError("[MagicLinks] MagicLinksNetworkManager needs a NetworkManager. Networked links stay local.", this);
                enabled = false;
                return;
            }

            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientStarted += OnClientStarted;
            networkManager.OnServerStopped += OnServerStopped;
            networkManager.OnClientStopped += OnClientStopped;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (networkManager.IsListening)
            {
                StartSession();
                if (networkManager.IsConnectedClient) OnClientConnected(networkManager.LocalClientId);
            }
        }

        private void OnDisable()
        {
            if (networkManager == null) return;

            networkManager.OnServerStarted -= OnServerStarted;
            networkManager.OnClientStarted -= OnClientStarted;
            networkManager.OnServerStopped -= OnServerStopped;
            networkManager.OnClientStopped -= OnClientStopped;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

            EndSession(string.Empty);
        }

        private void OnServerStarted() => StartSession();

        private void OnClientStarted() => StartSession();

        private void OnServerStopped(bool wasHost) => EndSession(string.Empty);

        private void OnClientStopped(bool wasHost) => EndSession(networkManager.DisconnectReason);

        private void OnClientConnected(ulong clientId) => _session?.OnClientConnected(clientId);

        private void OnClientDisconnected(ulong clientId)
        {
            // On a client, our own disconnection means the server is gone (or kicked us).
            if (networkManager.IsServer == false && clientId == networkManager.LocalClientId)
            {
                EndSession(networkManager.DisconnectReason);
                return;
            }

            _session?.OnClientDisconnected(clientId);
        }

        private void StartSession()
        {
            // A host raises both OnServerStarted and OnClientStarted.
            if (_session != null) return;

            foreach (string error in MagicLinksNetworkRegistry.GenerationErrors)
                Debug.LogError($"[MagicLinks] Network generation error: {error}");

            if (MagicLinksNetworkRegistry.GenerationErrors.Length > 0)
            {
                Debug.LogError("[MagicLinks] Networked links are NOT synchronized because of the errors above. " +
                               "Fix them in the MagicLinkEditor.");
            }

            _session = new MagicLinksNetworkSession(networkManager,
                (clientId, linkName) => GlobalWriteRule == null || GlobalWriteRule(clientId, linkName),
                () => MagicLinksNetwork.NotifySessionReady(networkManager.IsServer, networkManager.LocalClientId));

            MagicLinksNetworkRegistry.Registration?.Invoke(_session);

            _session.Start();
        }

        private void EndSession(string reason)
        {
            if (_session == null) return;

            _session.Stop();
            _session = null;

            MagicLinksNetwork.NotifySessionEnded(reason);
        }

        //----------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAutoAttach()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureAttached();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureAttached();

        private static void EnsureAttached()
        {
            if (MagicLinksNetworkRegistry.HasNetworkedLinks == false) return;

            NetworkManager singleton = NetworkManager.Singleton;
            if (singleton == null) return;

            if (FindAnyObjectByType<MagicLinksNetworkManager>(FindObjectsInactive.Include) != null) return;

            singleton.gameObject.AddComponent<MagicLinksNetworkManager>();
        }
    }
}
