using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Peg.UpdateSystem;
using Peg.AutoCreate;

namespace Peg.MessageDispatcher
{
    /// <summary>
    /// Global singleton that acts as a centralized hub for transmitting messages.
    /// It can be used to post global messages as well as forwarding targeted ones.
    /// 
    /// Messages can be instantaeous or delayed so that they process at the beginning
    /// of the next frame. They can also be buffered so that all listeners that come to
    /// the party late will still receive past messages.
    /// 
    /// This should not be placed on an Object. Instead, it will automatically instantiate
    /// a hidden GameObject for the lifetime of the application.
    /// 
    /// This object will not work outside of play-mode. Any calls to it from edit-mode will be ignored.
    /// </summary>
    /// 
    /// <remarks>
    /// This behaviour attempts to force itself to be the first object in the script update execution order.
    /// You should occasionally verify that this remains true to ensure proper and consistent functionality.
    /// </remarks>
    /// 
    ///
    [AutoCreate(resolvableTypes: typeof(IMessageDispatcher<GameObject>))]
    public partial class GlobalMessagePump : IPreUpdatable<SharedUpdateSystem>, IMessageDispatcher<GameObject>
    {
        static GlobalMessagePump _Instance;
        public static GlobalMessagePump Instance => _Instance;
        readonly AllPurposeMessageDispatcher Dispatcher = new();
        bool AppIsQuitting = false;

        public bool Enabled { get; set; } = true;


        /// <summary>
        /// Returns <c>true</c> if the message pump is not currently processing deferred messages, <c>false</c> otherwise.
        /// </summary>
        public bool IsPumpPaused
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying || AppIsQuitting) return true;
#endif
                return _Instance == null || !_Instance.Enabled;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void AutoAwake()
        {
            _Instance = this;
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnSystemInit(IUpdateSystem system)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Application.quitting += HandleAppQuit;
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnShutdown(IUpdateSystem system)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Update()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && !AppIsQuitting) Dispatcher.ProcessAllPendingMessages();
#else
            Dispatcher.ProcessAllPendingMessages();
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="mode"></param>
        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            //TODO: need a way to post persitent buffered messages for the rare cases where
            //we actually do want our messages to survive scene switches due to be posted by
            //persitent objects.
            if (mode == LoadSceneMode.Single)
                ClearAllMessages();
            //ClearPendingMessages(); this simply wasn't cutting it. Too many buggy 'ghost objects' after scene switching.
        }

        /// <summary>
        /// 
        /// </summary>
        void HandleAppQuit()
        {
            AppIsQuitting = true;
        }

        #region Static Methods
        /// <summary>
        /// Adds a listener of an event type to this message pump.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void AddListener<T>(MessageHandler<T> handler) where T : IMessage
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.AddListener(handler);
        }

        /// <summary>
        /// Adds a listener of an event type to this message pump.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void AddListener(Type msgType, MessageHandler handler)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.AddListener(msgType, handler);
        }

        /// <summary>
        /// Removes a listener of an event type from this message pump.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void RemoveListener<T>(MessageHandler<T> handler) where T : IMessage
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.RemoveListener(handler);
        }

        /// <summary>
        /// Removes a listener of an event type from this message pump.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void RemoveListener(Type msgType, MessageHandler handler)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.RemoveListener(msgType, handler);
        }

        /// <summary>
        /// Removes all listeners of all event types from this dispatcher.
        /// </summary>
        public void RemoveAllListeners()
        {
            Dispatcher.RemoveAllListeners();
        }

        /// <summary>
        /// Forwards the given message to the local message dispatch associated with the GameObject.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="localDispatch"></param>
        /// <param name="msg"></param>
        public void ForwardDispatch<T>(GameObject localDispatch, T msg) where T : IMessage
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.ForwardDispatch(localDispatch, msg);
        }

        /// <summary>
        /// Forwards the given message to the local message dispatch associated with the GameObject.
        /// </summary>
        /// <param name="localDispatch"></param>
        /// <param name="msg"></param>
        public void ForwardDispatch(GameObject localDispatch, Type msgType, IMessage msg)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.ForwardDispatch(localDispatch, msgType, msg);
        }

        /// <summary>
        /// Posts a message using a strategy based on what interfaces the message implements. Messages that implement
        /// IDeferedMessage will be processed on the next frame. IBufferedMessages will be buffered for all future listeners.
        /// Messages that implement both will be defered to the next frame and *then* buffered. Messages that implement neither
        /// will be dispatched immediately.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="msg"></param>
        public void PostMessage<T>(T msg) where T : IMessage
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            var dm = msg as IDeferredMessage;
            var bm = msg as IBufferedMessage;

            //Strategizing the type of message to post 
            //based on what interface(s) it implements.
            if (dm != null)
            {
                if (bm != null) Dispatcher.PostDelayedBufferedMessage(dm);
                else Dispatcher.PostDelayedMessage(dm);
            }
            else if (bm != null) Dispatcher.PostBufferedMessage(bm);
            else Dispatcher.PostMessage(msg);
        }

        /// <summary>
        /// Posts a message using a strategy based on what interfaces the message implements. Messages that implement
        /// IDeferedMessage will be processed on the next frame. IBufferedMessages will be buffered for all future listeners.
        /// Messages that implement both will be defered to the next frame and *then* buffered. Messages that implement neither
        /// will be dispatched immediately.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="msg"></param>
        public void PostMessage(Type msgType, IMessage msg)
        {
            //throw new UnityException("Not yet implmented.");

#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            var dm = msg as IDeferredMessage;
            var bm = msg as IBufferedMessage;

            //Strategizing the type of message to post 
            //based on what interface(s) it implements.
            if (dm != null)
            {
                if (bm != null) Dispatcher.PostDelayedBufferedMessage(msgType, dm);
                else Dispatcher.PostDelayedMessage(msgType, dm);
            }
            else if (bm != null) Dispatcher.PostBufferedMessage(msgType, bm);
            else Dispatcher.PostMessage(msgType, msg);
        }

        /// <summary>
        /// Registers an object a being associated to a local dispatcher. This allows multiple
        /// different GameObjects to be able to be used as forward targets that will redirect
        /// the message to the same dispatcher.
        /// </summary>
        /// <param name="dispatchOwner"></param>
        /// <param name="dispatcher"></param>
        public void RegisterLocalDispatch(GameObject dispatchOwner, IMessageDispatcher dispatcher)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.RegisterLocalDispatch(dispatchOwner, dispatcher);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dispatchOwner"></param>
        public void UnregisterLocalDispatch(GameObject dispatchOwner)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.UnregisterLocalDispatch(dispatchOwner);
        }

        /// <summary>
        /// Removes all internally pending, buffered, or pending-buffered messages.
        /// Use with caution.
        /// </summary>
        public void ClearAllMessages()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif
            Dispatcher.ClearAllMessages();
        }

        /// <summary>
        /// Rmoves all messages of the given type.
        /// </summary>
        /// <param name="msgType"></param>
        public void ClearMessagesOfType(Type msgType)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif
            Dispatcher.ClearMessagesOfType(msgType);
        }

        /// <summary>
        /// Removes all internally pending messages. Often used between scene switches.
        /// </summary>
        public void ClearPendingMessages()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.ClearPendingMessages();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="msg"></param>
        public void RemoveBufferedMessage<T>(T msg) where T : IBufferedMessage
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || AppIsQuitting) return;
#endif

            Dispatcher.RemoveBufferedMessage(msg);
        }
        #endregion

    }
}
