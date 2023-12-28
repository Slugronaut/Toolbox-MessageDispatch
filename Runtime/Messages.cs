using System;

namespace Peg.MessageDispatcher
{
    /// <summary>
    /// A simple message that can be posted by triggers when something touches them.
    /// </summary>
    public class TriggerEvent : IMessage { }


    /// <summary>
    /// Base class for deriving messages that supply a single target of an event.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete("Use TargetMessage<T,S> instead. It handles return types better.")]
    public abstract class TargetedEvent<T> : IMessage
    {
        /// <summary>
        /// The target that the event is happening to.
        /// </summary>
        public T Target { get; protected set; }
        public TargetedEvent(T target)
        {
            Target = target;
        }

        protected TargetedEvent() { }

        /// <summary>
        /// Used to internally change the target without requiring a recreation of this message object.
        /// When using this method be absolutely sure to cast to the correct message type or your message will be mistranslated as type of IMessage!
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual TargetedEvent<T> Change(T target)
        {
            Target = target;
            return this;
        }
    }


    /// <summary>
    /// Base class for deriving messages that supply two interrelated targets of an event.
    /// </summary>
    /// <typeparam name="A"></typeparam>
    /// <typeparam name="T"></typeparam>
    [Obsolete("Use AgentTargetMessage<T,S> instead. It handles return types better.")]
    public abstract class AgentTargetEvent<A, T> : IMessage
    {
        /// <summary>
        /// The agent that caused the the event.
        /// </summary>
        public A Agent { get; protected set; }
        /// <summary>
        /// The target that the event is happening to.
        /// </summary>
        public T Target { get; protected set; }
        public AgentTargetEvent(A agent, T target)
        {
            Agent = agent;
            Target = target;
        }

        protected AgentTargetEvent() { }

        /// <summary>
        /// Used to internally change the target and agent without requiring a recreation of this message object.
        /// WARNING: !! When using this method be absolutely sure to cast to the correct message type or your message will be mistranslated as type of IMessage!
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual AgentTargetEvent<A, T> Change(A agent, T target)
        {
            Agent = agent;
            Target = target;
            return this;
        }
    }


    /// <summary>
    /// Base class for deriving messages that supply a single target of an event.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class TargetMessage<T, S> : IMessage where S : TargetMessage<T, S>//, new()
    {
        /// <summary>
        /// The target that the event is happening to.
        /// </summary>
        public T Target { get; protected set; }
        public TargetMessage(T target)
        {
            Target = target;
        }

        protected TargetMessage() { }

        /// <summary>
        /// Used to internally change the target without requiring a recreation of this message object.
        /// When using this method be absolutely sure to cast to the correct message type or your message will be mistranslated as type of IMessage!
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual S Change(T target)
        {
            Target = target;
            return this as S;
        }
    }


    /// <summary>
    /// Base class for deriving messages that supply two interrelated targets of an event.
    /// </summary>
    /// <typeparam name="A"></typeparam>
    /// <typeparam name="T"></typeparam>
    public abstract class AgentTargetMessage<A, T, S> : IMessage where S : AgentTargetMessage<A, T, S>, new()
    {
        /// <summary>
        /// The agent that caused the the event.
        /// </summary>
        public A Agent { get; protected set; }
        /// <summary>
        /// The target that the event is happening to.
        /// </summary>
        public T Target { get; protected set; }
        public AgentTargetMessage(A agent, T target)
        {
            Agent = agent;
            Target = target;
        }

        protected AgentTargetMessage() { }

        /// <summary>
        /// Used to internally change the target and agent without requiring a recreation of this message object.
        /// WARNING: !! When using this method be absolutely sure to cast to the correct message type or your message will be mistranslated as type of IMessage!
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public virtual S Change(A agent, T target)
        {
            Agent = agent;
            Target = target;
            return this as S;
        }
    }

}
