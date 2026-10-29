using EntityStates;
using RoR2;
using RoR2Randomizer.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.CustomContent
{
    public sealed class MultiEntityState : EntityState
    {
        public class StateMachineSubStatesData : MonoBehaviour
        {
            public SerializableEntityStateType[] StateTypes = Array.Empty<SerializableEntityStateType>();
            public bool AlwaysExecuteEnter;
            public float MinActiveDuration;
        }

        readonly struct SubState
        {
            public readonly EntityState EntityState;
            public readonly Type CachedStateType;

            public SubState(EntityState state)
            {
                EntityState = state;
                CachedStateType = state.GetType();
            }

            SubState(EntityStateIndex index)
            {
                EntityState = EntityStateCatalog.InstantiateState(CachedStateType = EntityStateCatalog.GetStateType(index));
            }

            public SubState(NetworkReader reader) : this(reader.ReadEntityStateIndex())
            {
                EntityState.OnDeserialize(reader);
            }

            public void Serialize(NetworkWriter writer)
            {
                writer.Write(EntityStateCatalog.GetStateIndex(CachedStateType));
                EntityState.OnSerialize(writer);
            }
        }

        public static readonly SerializableEntityStateType SerializableStateType = new SerializableEntityStateType(typeof(MultiEntityState));

        SubState[] _subStates = Array.Empty<SubState>();

        float _minActiveDuration;
        float _enterExecuteTime = -1f;

        bool _alwaysExecuteEnter;

        bool _isWaitingForForceExitDestruction;
        public bool IsWaitingForForceExitDestruction => _isWaitingForForceExitDestruction;

        public MultiEntityState()
        {
        }

        public void Initialize(StateMachineSubStatesData data)
        {
            _subStates = data.StateTypes
                .Select(type => EntityStateCatalog.InstantiateState(data.GetType()))
                .Select(state => new SubState(state))
                .ToArray();

            _alwaysExecuteEnter = data.AlwaysExecuteEnter;

            _minActiveDuration = data.MinActiveDuration;
        }

        public void OnOuterStateMachineAssigned()
        {
            foreach (SubState subState in _subStates)
            {
                subState.EntityState.outer = outer;

                if (subState.EntityState is MultiEntityState multiState)
                {
                    multiState.OnOuterStateMachineAssigned();
                }
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)_subStates.Length);
            foreach (SubState subState in _subStates)
            {
                subState.Serialize(writer);
            }

            writer.Write(_minActiveDuration);

            writer.WriteBits(_alwaysExecuteEnter, _isWaitingForForceExitDestruction);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            uint length = reader.ReadPackedUInt32();

            _subStates = new SubState[length];
            for (uint i = 0; i < length; i++)
            {
                _subStates[i] = new SubState(reader);
            }

            _minActiveDuration = reader.ReadSingle();

            reader.ReadBits(out _alwaysExecuteEnter, out _isWaitingForForceExitDestruction);
        }

        public override void OnEnter()
        {
            onEnter(null);
        }

        void onEnter(HashSet<Type> exclude)
        {
            foreach (SubState subState in _subStates)
            {
                if (exclude == null || !exclude.Contains(subState.CachedStateType))
                {
                    subState.EntityState.OnEnter();
                }
            }

            _enterExecuteTime = Time.time;
        }

        internal void forceExit(HashSet<Type> excludeExitTypes, HashSet<Type> excludeEnterTypes)
        {
            if (_subStates == null)
                return;

            if (_alwaysExecuteEnter && _enterExecuteTime < 0f)
            {
#if DEBUG
                Log.Debug("Attempting to exit state without OnEnter, executing enter now");
#endif
                onEnter(excludeEnterTypes);
            }

            foreach (SubState subState in _subStates)
            {
                if (subState.EntityState != null && (excludeExitTypes == null || !excludeExitTypes.Contains(subState.CachedStateType)))
                {
                    try
                    {
                        subState.EntityState.OnExit();
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"Caught exception when exiting {nameof(MultiEntityState)} due to state machine destroyed: {e}");
                    }
                }
            }

            IEnumerator routine = waitThenDestroyFromForceExit();
            if (outer)
            {
                outer.StartCoroutine(routine);
            }
            else
            {
                Main.Instance.StartCoroutine(routine);
            }
        }

        IEnumerator waitThenDestroyFromForceExit()
        {
            _isWaitingForForceExitDestruction = true;

            float timeSinceEnter = Time.time - _enterExecuteTime;
            if (timeSinceEnter < _minActiveDuration)
            {
                yield return new WaitForSeconds(_minActiveDuration - timeSinceEnter);
            }

            _isWaitingForForceExitDestruction = false;
            GameObject.Destroy(outer.gameObject);
            yield break;
        }

        public override void OnExit()
        {
            foreach (SubState subState in _subStates)
            {
                subState.EntityState.OnExit();
            }
        }

        public override void ModifyNextState(EntityState nextState)
        {
            foreach (SubState subState in _subStates)
            {
                subState.EntityState.ModifyNextState(nextState);
            }
        }

        public override void Update()
        {
            foreach (SubState subState in _subStates)
            {
                subState.EntityState.Update();
            }
        }

        public override void FixedUpdate()
        {
            foreach (SubState subState in _subStates)
            {
                subState.EntityState.FixedUpdate();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return (InterruptPriority)_subStates.Select(subState => (int)subState.EntityState.GetMinimumInterruptPriority()).Max();
        }

        public override void PlayAnimation(string layerName, string animationStateName)
        {
            foreach (SubState subState in _subStates)
            {
                subState.EntityState.PlayAnimation(layerName, animationStateName);
            }
        }
    }
}
