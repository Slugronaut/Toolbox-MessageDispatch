/**
 * Copyright 2008-2016
 * James Clark
 * 
 * UPDATED: 1/24/2012 - Added one function call from Unity. Debug.Log(). Otherwise, still entirely portable.
 * UPDATE: 6/1/2014 - Added a new class of message called 'Demands'. Used mostly by Unity to replace use of GetComponent<> in time-critical sections.
 * UPDATE: 4/16/2016 - Added to a new namespace called 'Toolbox'.
 *                   - Replaced 'Dictionary' with 'Toolbox.HashMap' in the 'AllPurposeMessageDispatcher'.
 *                     Trying to reduce garbage in Unity due to foreach iteration when flushing the deferred message pump.
 * UPDATED: 12/16/2016 - Added unity assertion checks.
 * 
 * NOTE: 1/15/2017 - Unity 5.5 has removed some GC issues with foreach loops. Might be able to revert back to standard Dictionary for deferred messages.
 */
 
//Currently broken in Unity
//#define TOOLBOX_FASTDISPATCH
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Toolbox
{
    /*
     * TYPES OF MESSAGES:
     * Messages are segregated into four sub-types. These sub-types don't have any mechanical purpose but are simply
     * semantics used to organize the meaning and useage for each type of message.
     * 
     * Events: Events are used by an object to inform other interested parties about internal state changes.
     *         For example, when a health object changes it might post a 'HealthChangedEvent'.
     *         
     * Commands: Commands are messages posted by other interested parties that listeners then use to execute a relevant action upon receiving.
     *           Another example for a health object would be a 'ChangeHealthCommand'. It would be posted by another object (combat system, for example)
     *           and upon recieving such a message, the health object would react accordingly by altering its internal health state.
     *           
     * Request/Response: Usually comes in pairs. An object that handles a request message is usually expected to issue a response message in return.
     *                   An alternative may be that the one that posts the request will also provide a reference to itself in the message
     *                   so that a direct response may be sent back from the receiver.
     *                   
     * Demand: Similar to Request/Response but the issuer of the demand instead provides a callback method that is expected to be invoked
     *         by the recipient. This system is also similar to the 'promise pattern' except that there is no concurrency involved and the
     *         callback is expected to be invoked immediately.
     */


    /// <summary>
    /// Interface that all dispatchable messages must implement. There are no
    /// public methods or properties, it is simply used to ensure proper 
    /// type-constraining within the MessageDispatcher classes.
    /// </summary>
	public interface IMessage {}

    //Various semantics for messages. These types don't actually influence anything currently but instead imply the intended usage.
    public interface IMessageEvent : IMessage {}
    public interface IMessageCommand : IMessage {}
    public interface IMessageRequest : IMessage {}
    public interface IMessageResponse : IMessage {}


    /// <summary>
    /// Specialized message that is structured for Demand/Callback systems.
    /// </summary>
    public interface IDemand<T> : IMessage
    {
        void Respond(T desired);
    }

    /// <summary>
    /// Special-case subclass message that implies its processing should be deferred
    /// to a later point than when it was sent. Can be used by message dispatchers 
    /// to strategize how/when to process this type of message.
    /// </summary>
    public interface IDeferredMessage : IMessage {}

    /// <summary>
    /// Special-case subclass message that implies it should be kept on record internally
    /// and immediately dispatched to any new listeners that register for that type of message.
    /// Can be used by message dispatchers to strategize how/when to process this type of message.
    /// </summary>
    public interface IBufferedMessage : IMessage {}

    /// <summary>
    /// A special-case subclass message that implies it should post exactly
    /// one instance of that message. Subsiquent postings will be ignored
    /// until the relevant dispatcher is cleared.
    /// </summary>
    public interface IOneshotMessage : IMessage { }



    /// <summary>
    /// Delegate for defining a generic statically-typed message handler.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="message"></param>
    public delegate void MessageHandler<in T>(T message) where T : IMessage;


    /// <summary>
    /// Delegate for defining a non-generic runtime-typed message handler.
    /// </summary>
    /// <param name="msgType"></param>
    /// <param name="message"></param>
    public delegate void MessageHandler(Type msgType, object message);


    /// <summary>
    /// Interface exposed by all message dispatchers that work with <see cref="IMessage"/> and <see cref="MessageHandler"/>s.
    /// </summary>
	public interface IMessageDispatcher
	{
		void AddListener<T>(MessageHandler<T> handler) where T : IMessage;
        void AddListener(Type msgType, MessageHandler handler);
        void RemoveListener<T>(MessageHandler<T> handler) where T : IMessage;
        void RemoveListener(Type msgType, MessageHandler handler);
        void RemoveAllListeners();
		void PostMessage<T>(T message) where T : IMessage;
        void PostMessage(Type msgType, IMessage message);

        void RegisterLocalDispatch(object owner, IMessageDispatcher dispatcher);
        void UnregisterLocalDispatch(object owner);
        void ForwardDispatch<T>(object owner, T message) where T : IMessage;
        void ForwardDispatch(object owner, Type msgType, IMessage message);
        void ClearAllMessages();
        void ClearMessagesOfType(Type msgType);
	}

    /// <summary>
    /// Interface exposed by all message dispatchers that work with <see cref="IMessage"/> and <see cref="MessageHandler"/>s.
    /// </summary>
	public interface IMessageDispatcher<O>
    {
        void AddListener<T>(MessageHandler<T> handler) where T : IMessage;
        void AddListener(Type msgType, MessageHandler handler);
        void RemoveListener<T>(MessageHandler<T> handler) where T : IMessage;
        void RemoveListener(Type msgType, MessageHandler handler);
        void RemoveAllListeners();
        void PostMessage<T>(T message) where T : IMessage;
        void PostMessage(Type msgType, IMessage message);

        void RegisterLocalDispatch(O owner, IMessageDispatcher dispatcher);
        void UnregisterLocalDispatch(O owner);
        void ForwardDispatch<T>(O owner, T message) where T : IMessage;
        void ForwardDispatch(O owner, Type msgType, IMessage message);
        void ClearAllMessages();
        void ClearMessagesOfType(Type msgType);
    }

    /// <summary>
    /// Interface exposed by specializeded message dispatchers that support removal of buffered messages.
    /// </summary>
    public interface IBufferedMessageDispatcher
    {
        bool HasBufferedMessages { get; }
        void PostBufferedMessage<T>(T message) where T : IBufferedMessage;
        void RemoveBufferedMessage<T>(T msg) where T : IBufferedMessage;
        void ClearBufferedMessages();
    }

    /// <summary>
    /// Interface exposed by specialized messaged dispatchers that support starting and stopping of the message pump.
    /// </summary>
    public interface IDeferredMessageDispatcher
    {
        bool HasPendingMessages { get; }
        void PostDelayedMessage<T>(T message) where T : IDeferredMessage;
        void ProcessAllPendingMessages();
        void ClearPendingMessages();
    }

	/// <summary>
    /// Instantaneously dispatches messages to all listeners of that type of message.
    /// </summary>
	public class InstantMessageDispatcher : IMessageDispatcher
	{
        /// <summary>
        /// Defines the initial size that should be used by most listener lists and dictionaries.
        /// </summary>
        protected const int PreAllocSize = 10;
        readonly Dictionary<Type, Delegate> Listeners = new Dictionary<Type, Delegate>(PreAllocSize);
        readonly Dictionary<Type, MessageHandler> RuntimeListeners = new Dictionary<Type, MessageHandler>(PreAllocSize);
        readonly Dictionary<object, IMessageDispatcher> LocalDispatches = new Dictionary<object, IMessageDispatcher>();
        readonly HashSet<Type> UniqueMessageTypes = new HashSet<Type>();


        /// <summary>
        /// Removes all internal references to the given type of message.
        /// This will clear the unique message cache as well as all relevant listeners.
        /// </summary>
        /// <param name="msgType"></param>
        public virtual void ClearMessagesOfType(Type msgType)
        {
            UniqueMessageTypes.Clear();
            Listeners.Remove(msgType);
            RuntimeListeners.Remove(msgType);
        }

        /// <summary>
        /// Removes any interally stored messages states.
        /// </summary>
        public virtual void ClearAllMessages()
        {
            UniqueMessageTypes.Clear();
        }

        /// <summary>
        /// Conditional method that asserts a message is not null.
        /// </summary>
        /// <param name="msg"></param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void IsMessageNotNull(IMessage msg)
        {
            if (msg == null) throw new AssertionException("Message is null.", "Expected not null.");
        }

        /// <summary>
        /// Conditional method that asserts a message is not null.
        /// </summary>
        /// <param name="msg"></param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void IsDispatcherNotNull(IMessageDispatcher dispatch)
        {
            if (dispatch == null) throw new AssertionException("Message is null.", "Expected not null.");
        }

        /// <summary>
        /// Adds a handler for an event type to this dispatcher.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
		public virtual void AddListener<T> (MessageHandler<T> handler) where T : IMessage
		{
            Assert.IsNotNull(handler);

            Delegate del;
			if(Listeners.TryGetValue(typeof(T), out del))
				Listeners[typeof(T)] = Delegate.Combine(del, handler);
			else Listeners[typeof(T)] = handler;
		}

        /// <summary>
        /// Adds a handler for an event type to this dispatcher.
        /// </summary>
        public virtual void AddListener(Type msgType, MessageHandler handler)
        {
            Assert.IsNotNull(msgType);
            Assert.IsNotNull(handler);

            MessageHandler del;
            if (RuntimeListeners.TryGetValue(msgType, out del))
                RuntimeListeners[msgType] = MessageHandler.Combine(del, handler) as MessageHandler;
            else RuntimeListeners[msgType] = handler;
        }

        /// <summary>
        /// Removes a handler for an event type from this dispatcher.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
		public virtual void RemoveListener<T> (MessageHandler<T> handler) where T : IMessage
		{
            Assert.IsNotNull(handler);


            Delegate del;
            if (Listeners.TryGetValue(typeof(T), out del))
            {
                var d = Delegate.Remove(del, handler);
                if (d == null) Listeners.Remove(typeof(T));
                else Listeners[typeof(T)] = d;
            }
            else Listeners.Remove(typeof(T));
		}

        /// <summary>
        /// Removes a handler for an event type from this dispatcher.
        /// </summary>
        /// <param name="handler"></param>
        public virtual void RemoveListener(Type msgType, MessageHandler handler)
        {
            Assert.IsNotNull(msgType);
            Assert.IsNotNull(handler);

            MessageHandler del;
            if (RuntimeListeners.TryGetValue(msgType, out del))
            {
                var d = MessageHandler.Remove(del, handler);
                if (d == null) RuntimeListeners.Remove(msgType);
                else RuntimeListeners[msgType] = d as MessageHandler;
            }
            else RuntimeListeners.Remove(msgType);
        }

        /// <summary>
        /// Removes all message handlers for all types of events from this dispatcher.
        /// </summary>
		public virtual void RemoveAllListeners()
		{
            var types = new Type[Listeners.Keys.Count];
            Listeners.Keys.CopyTo(types, 0);

            for (int i = 0; i < types.Length; i++)
            {
                Delegate[] dels = Listeners[types[i]].GetInvocationList();
                foreach (var del in dels)
                {
                    var newHandler = Delegate.Remove(Listeners[types[i]], del);
                    if (newHandler == null) Listeners.Remove(types[i]);
                    else Listeners[types[i]] = newHandler;
                }
            }
		}

        /// <summary>
        /// Instantly dispatches the message to all handlers listening for that type of message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
		public virtual void PostMessage<T> (T message) where T : IMessage
		{
            IsMessageNotNull(message);
            if (!IsDuplicateUniqueMessage(typeof(T)))
                HandleMessage<T>(message);
		}

        /// <summary>
        /// Instantly dispatches the message to all handlers listening for that type of message.
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="message"></param>
		public virtual void PostMessage(Type msgType, IMessage message)
        {
            IsMessageNotNull(message);
            if (!IsDuplicateUniqueMessage(msgType))
                HandleMessage(msgType, message);
        }

        /// <summary>
        /// Checks to see if the given message type is a unique message.
        /// Unique messages can only post once without clearing the dispatcher.
        /// </summary>
        /// <param name="msgType"></param>
        /// <returns></returns>
        public bool IsDuplicateUniqueMessage(Type msgType)
        {
            if (msgType.IsSubclassOf(typeof(IOneshotMessage)))
            {
                if (!UniqueMessageTypes.Contains(msgType))
                {
                    UniqueMessageTypes.Add(msgType);
                    return false;
                }
                else return true;
            }
            else return false;
        }

        /// <summary>
        /// Forwards the message to a local message dispatch associated with the given object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="owner"></param>
        /// <param name="message"></param>
        public void ForwardDispatch<T>(object owner, T message) where T : IMessage
        {
            Assert.IsNotNull(owner);
            IsMessageNotNull(message);

            IMessageDispatcher dispatch;
            if (LocalDispatches.TryGetValue(owner, out dispatch))
                dispatch.PostMessage(message);
        }

        /// <summary>
        /// Forwards the message to a local message dispatch associated with the given object.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="msgType"></param>
        /// <param name="message"></param>
        public void ForwardDispatch(object owner, Type msgType, IMessage message)
        {
            Assert.IsNotNull(owner);
            IsMessageNotNull(message);

            IMessageDispatcher dispatch;
            if (LocalDispatches.TryGetValue(owner, out dispatch))
                dispatch.PostMessage(msgType, message);
        }

        /// <summary>
        /// Registers an object that has a local dispatch mechanism associated with it.
        /// This allows a global dispatch system to target individuals.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="dispatcher"></param>
        public void RegisterLocalDispatch(object owner, IMessageDispatcher dispatcher)
        {
            Assert.IsNotNull(owner);
            IsDispatcherNotNull(dispatcher);

            LocalDispatches[owner] = dispatcher;
        }

        /// <summary>
        /// Unregisters a previously registered object's local message dispatcher.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="dispatcher"></param>
        public void UnregisterLocalDispatch(object owner)
        {
            Assert.IsNotNull(owner);
            LocalDispatches.Remove(owner);
        }

        /// <summary>
        /// Internal helper method for handling posted messages.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        protected void HandleMessage<T>(T message) where T : IMessage
        {
            IsMessageNotNull(message);
            Type t = typeof(T);

            //generic statically-typed listeners
            Delegate del;
            if (Listeners.TryGetValue(t, out del))
            {
                var callback = del as MessageHandler<T>;
                if (callback != null) callback(message);
            }


            //non-generic, dynamically-typed listeners
            MessageHandler hand;
            if(RuntimeListeners.TryGetValue(t, out hand))
            {
                var callback = hand as MessageHandler;
                if (callback != null) callback(t, message);
            }
        }

		/// <summary>
		/// Internal helper method for handling posted messages.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="message"></param>
		protected void HandleMessage(Type msgType, IMessage message)
		{
            Assert.IsNotNull(msgType);
            IsMessageNotNull(message);

            //generic, statically-typed listeners
            Delegate del;
			if (Listeners.TryGetValue(msgType, out del))
			{
                if (del == null)
                {
                    //we missed cleanup somewhere
#if DEBUG && UNITY_EDITOR
                    UnityEngine.Debug.Log("<color=red>There are no valid delegates for the message type '" + msgType.Name + "' in the dispatcher. This indicates some kind of issue with removing all listeners for a given type of message.</color>");
#endif
                    Listeners.Remove(msgType);
                }
                else del.DynamicInvoke(message);
			}

            //non-generic, dynamically-typed listeners
            MessageHandler hand;
            if (RuntimeListeners.TryGetValue(msgType, out hand))
            {
                var callback = hand as MessageHandler;
                if (callback != null) callback(msgType, message);
            }
		}
	}


    /// <summary>
	/// Message dispatcher that can process messages in an instantaneous, delayed, buffered, and delayed-buffered fashion.
    /// It can also forward messages to local dispatchers on specific GameObjects.
	/// </summary>
	public class AllPurposeMessageDispatcher : InstantMessageDispatcher, IDeferredMessageDispatcher, IBufferedMessageDispatcher
	{
        //readonly HashMap<Type, Queue<IMessage>> PendingMessages = new(PreAllocSize);
        readonly Dictionary<Type, Queue<IMessage>> PendingMessages = new(PreAllocSize);
        readonly Dictionary<Type, List<IMessage>> BufferedMessages = new(PreAllocSize);


        //This is used when we post delayed, buffered messages because we can't buffer until
        //we actually post the message, and the posting is... well... delayed!
        readonly List<IMessage> BufferOnDelayMessages = new(PreAllocSize);

        public bool HasPendingMessages
		{
			get
			{
				foreach(var kvp in PendingMessages)
				{
					if (kvp.Value.Count > 0) return true;
				}
				return false;
			}
		}

		public bool HasBufferedMessages
		{
			get
			{
				foreach (var kvp in BufferedMessages)
				{
					if (kvp.Value.Count > 0) return true;
				}
				return false;
			}
		}

        /// <summary>
        /// Removes a previously buffered or delayed-buffered message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="msg"></param>
		public void RemoveBufferedMessage<T>(T msg) where T : IBufferedMessage
		{
            IsMessageNotNull(msg);

			//if it was still pending, we don't need to look in the buffered messages
			if(BufferOnDelayMessages.Remove(msg as IMessage)) return;

            //HUGE BUG!: After a scene switch, any messages that were previously buffered
            //by an object that has since been destroyed will remain in the system but
            //TryGetValue will return false. These messages are the reason we later see
            //null reference errors pop up in message handlers after switching scenes
            //or ending playmode.
			List<IMessage> list;
			if(BufferedMessages.TryGetValue(typeof(T), out list))
			{
                list.Remove(msg as IMessage);
				if(list.Count == 0)
					BufferedMessages.Remove(typeof(T));
				else BufferedMessages[typeof(T)] = list;
			}
		}

		/// <summary>
		/// Removes all pending messages. Pending buffered messages are ignored.
		/// </summary>
		public void ClearPendingMessages()
		{
			PendingMessages.Clear();
		}

		/// <summary>
		/// Removes all previously posted events from the buffer.
		/// If they are delayed-buffered messages they will also be removed.
		/// </summary>
		public void ClearBufferedMessages()
		{
			BufferedMessages.Clear();
			BufferOnDelayMessages.Clear();
		}

		/// <summary>
		/// Removes all buffered messages that are still pending and
		/// have yet to be buffered. Non-buffered pending messages
		/// are ignored.
		/// </summary>
		public void ClearPendingBufferedMessages()
		{
			BufferOnDelayMessages.Clear();
		}

        /// <summary>
        /// Removes any internally stored message states, unique, pending, and buffered messages.
        /// </summary>
        public override void ClearAllMessages()
        {
            base.ClearAllMessages();
            PendingMessages.Clear();
            BufferedMessages.Clear();
            BufferOnDelayMessages.Clear();
        }

        /// <summary>
        /// Removes any internally stored references to the given type of message.
        /// </summary>
        /// <param name="msgType"></param>
        public override void ClearMessagesOfType(Type msgType)
        {
            base.ClearAllMessages();
            BufferedMessages.Remove(msgType);
            PendingMessages.Remove(msgType);
        }

		/// <summary>
		/// Adds a handler for an event type and then sends all previously buffered messages
		/// of that type to this handler.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="handler"></param>
		public override void AddListener<T>(MessageHandler<T> handler)
		{
			//add listener as normal
			base.AddListener<T>(handler);

			//if this type of message has been invoked before, invoke for this listener now
			List<IMessage> list;
			if (BufferedMessages.TryGetValue(typeof(T), out list))
			{
                T msg;
				for (int i = 0; i < list.Count; i++)
				{
#if TOOLBOX_FASTDISPATCH
                    handler((T)list[i]);
#else
                    msg = (T)list[i];
                    if (msg != null) handler(msg);
#endif
                }
			}
		}

        /// <summary>
        /// Adds a handler for an event type and then sends all previously buffered messages
        /// of that type to this handler.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public override void AddListener(Type msgType, MessageHandler handler)
        {
            //add listener as normal
            base.AddListener(msgType, handler);

            //if this type of message has been invoked before, invoke for this listener now
            List<IMessage> list;
            if (BufferedMessages.TryGetValue(msgType, out list))
            {
                IMessage msg;
                for (int i = 0; i < list.Count; i++)
                {
#if TOOLBOX_FASTDISPATCH
                    handler(msgType, list[i]);
#else
                    msg = list[i];
                    if (msg != null) handler(msgType, msg);
#endif
                    
                }
            }
        }

		/// <summary>
		/// Stores a message for later dispatchment.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="message"></param>
		public virtual void PostDelayedMessage<T>(T message) where T : IDeferredMessage
		{
            PostDelayedMessage(typeof(T), message);
		}

        /// <summary>
        /// Stores a message for later dispatchment.
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="message"></param>
        public virtual void PostDelayedMessage(Type msgType, IMessage message)
        {
            IsMessageNotNull(message);

            if(IsDuplicateUniqueMessage(msgType)) return;

            Queue<IMessage> list;
            if (PendingMessages.TryGetValue(msgType, out list))
                list.Enqueue(message);
            else
            {
                var q = new Queue<IMessage>();
                q.Enqueue(message);
                PendingMessages[msgType] = q;
            }
        }

		/// <summary>
		/// Dispatches a message to all concerned listeners of that type and then
		/// stores it in a buffer. Listeners that attach to this dispatcher at a later
		/// time will receive all previously buffered messages.
		/// </summary>
		/// <param name="message">Message.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public virtual void PostBufferedMessage<T>(T message) where T : IBufferedMessage
		{
            if (IsDuplicateUniqueMessage(typeof(T))) return;
            base.PostMessage<T>(message);
			if (BufferedMessages.ContainsKey(typeof(T)))
				BufferedMessages[typeof(T)].Add(message);
			else
			{
				var list = new List<IMessage>(PreAllocSize);
				list.Add(message);
				BufferedMessages[typeof(T)] = list;
			}
		}

        /// <summary>
        /// Dispatches a message to all concerned listeners of that type and then
        /// stores it in a buffer. Listeners that attach to this dipatcher at a later
        /// time will receive all previously buffered messages.
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="message"></param>
        public virtual void PostBufferedMessage(Type msgType, IMessage message)
        {
            if (IsDuplicateUniqueMessage(msgType)) return;
            base.PostMessage(msgType, message);
            if (BufferedMessages.ContainsKey(msgType))
                BufferedMessages[msgType].Add(message);
            else
            {
                var list = new List<IMessage>(PreAllocSize);
                list.Add(message);
                BufferedMessages[msgType] = list;
            }
        }

		/// <summary>
		/// Stores a message for later dispatchment. The message will also be flagged so that when it
		/// finally is dispatched it will also be buffered. Listeners that attach to this dispatcher at a later
		/// time will receive all previously buffered messages.
		/// </summary>
		/// <param name="message">Message.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public virtual void PostDelayedBufferedMessage<T>(T message) where T : IDeferredMessage
		{
            if (IsDuplicateUniqueMessage(typeof(T))) return;
            BufferOnDelayMessages.Add(message);
            PostDelayedMessage<T>(message);
		}

        /// <summary>
        /// Stores a message for later dispatchment. The message will also be flagged so that when it
		/// finally is dispatched it will also be buffered. Listeners that attach to this dispatcher at a later
		/// time will receive all previously buffered messages.
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="message"></param>
        public virtual void PostDelayedBufferedMessage(Type msgType, IMessage message)
        {
            if (IsDuplicateUniqueMessage(msgType)) return;
            BufferOnDelayMessages.Add(message);
            PostDelayedMessage(msgType, message);
        }

        /// <summary>
        /// Dispatches all messages that have been posted since the last time this method was called.
        /// The messages will be dispatched in the order they were receieved. Any messages that were
        /// flagged to be buffered but had not yet been processed will become buffered at this point.
        /// </summary>
        public virtual void ProcessAllPendingMessages()
		{
            //NOTE: Switched to using a custom dictionary that exposes direct access to keys.
            //This was done to avoid using the enumerator in standard dictionary since Unity
            //produces garbage when iterating.
            //
            //TODO: This needs to have some performance testing done
            //at some point to see if the custom dictionary's performance overall matches
            //the standard one - if not, then the cost of accessing values might outweigh
            //the benefit of not using enumeration.
            //
            //UPDATE: Unity 5.5 has fixed many issues with garbage in foreach loops! Might be able
            //to use a stardard Dictionary again!
            //
            // UPDATE: back to using Dictionray with Foreach
            //
            foreach (var queue in PendingMessages.Values)
            {
                //a broken aspect of using 'SimpleKeys' is that it will return all elements of the dictionary's
                //internal array, including pre-allocated null buckets. We need to check for that here.
                if (queue == null || queue.Count < 1) continue;
                while (queue.Count > 0)
                {
                    var msg = queue.Dequeue();
                    var msgType = msg.GetType();

                    //BUG FIX: 5/10/2017
                    //The new multithreaded object deserialization was actually kicking in here
                    //and causes objects that subscribed to Deferred-buffered messages to actually miss them
                    //because the message would get posted, the object would call 'Awake()' where they would
                    //subscribe and then then message would be buffered.
                    //Switching the order (buffer *then* post) fixed it.
                    //TODO: a better way would probably be to invoke all listeners just after buffering?
                        

                    //only buffer the message if it was supposed to be buffered but couldn't be
                    //when posted because it was also a delayed message
                    if (BufferOnDelayMessages.Contains(msg))
                    {
                        BufferOnDelayMessages.Remove(msg);
                        if (BufferedMessages.ContainsKey(msgType))
                            BufferedMessages[msgType].Add(msg);
                        else
                        {
                            var list = new List<IMessage>(PreAllocSize);
                            list.Add(msg);
                            BufferedMessages[msgType] = list;
                        }
                    }


                    //handle the message
                    HandleMessage(msgType, msg);
                }

            }


			//remove all pending messages
			PendingMessages.Clear();

		}

	}
}
