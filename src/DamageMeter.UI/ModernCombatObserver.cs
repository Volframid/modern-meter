using System;
using System.IO;
using Data;
using Tera;
using Tera.Game;
using Tera.Sniffing;

namespace DamageMeter.UI;

// Read-only observation of the existing stream; no hooks, injection or duplicate full packet parser.
public sealed class ModernCombatObserver : IDisposable
{
    private readonly ITeraSniffer _sniffer;
    private readonly Func<ulong?> _self;
    private readonly Action<bool> _changed;
    private readonly Func<uint, string, ushort> _opcode;
    private readonly Func<ulong?> _target;
    private readonly object _gate = new();
    private uint _version;
    private ushort _status, _load, _life, _despawn, _occupier;
    private bool _characterInCombat, _awaitingBoss, _suppressStance;
    private ulong? _activeBoss, _finishedBoss;
    private bool _canRepull;

    public bool IsInCombat { get { lock (_gate) return GetCombatState(); } }

    private bool GetCombatState()
    {
        var target = _target();
        if (target.HasValue && target != _finishedBoss && (_characterInCombat || _activeBoss.HasValue || _awaitingBoss))
        {
            _activeBoss = target;
            _awaitingBoss = false;
        }
        return _activeBoss.HasValue ||
            (_characterInCombat && (!_finishedBoss.HasValue || (target == null && !_suppressStance)));
    }

    private void FinishBoss(ulong id, bool canRepull)
    {
        _activeBoss = null;
        _awaitingBoss = false;
        _suppressStance = true;
        _finishedBoss = id;
        _canRepull = canRepull;
        _changed(false);
    }

    private void ClearEncounter()
    {
        _characterInCombat = false;
        _awaitingBoss = false;
        _suppressStance = false;
        _activeBoss = null;
        _finishedBoss = _target();
        _canRepull = true;
        _changed(false);
    }

    public ModernCombatObserver(ITeraSniffer sniffer, Func<ulong?> self, Action<bool> changed,
        Func<uint, string, ushort>? opcode = null, Func<ulong?>? target = null)
    {
        _sniffer = sniffer; _self = self; _changed = changed;
        _target = target ?? (() => null);
        _opcode = opcode ?? ((version, name) => new OpCodeNamer(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/{version}.txt")).GetCode(name));
        sniffer.MessageReceived += Observe; sniffer.EndConnection += Reset; sniffer.NewConnection += OnConnection;
    }
    private void OnConnection(Server server) => Reset();
    private void Reset()
    {
        lock (_gate)
        {
            _version = 0;
            _status = _load = _life = _despawn = _occupier = 0;
            ClearEncounter();
        }
    }
    public void TryConfigure()
    {
        try
        {
            lock (_gate)
            {
                if (_version == 0 || _status != 0) return;
                _load = _opcode(_version, "S_LOAD_TOPO");
                _life = _opcode(_version, "S_CREATURE_LIFE");
                _despawn = _opcode(_version, "S_DESPAWN_NPC");
                _occupier = _opcode(_version, "S_NPC_OCCUPIER_INFO");
                _status = _opcode(_version, "S_USER_STATUS");
            }
        }
        catch { /* Optional status stays unavailable until the map is readable. */ }
    }

    private void Observe(Message message)
    {
        try
        {
            lock (_gate)
            {
                var opcode = message.OpCode;
                if (opcode == 19900)
                {
                    ReadVersion(message);
                    return;
                }
                if (_load != 0 && opcode == _load)
                {
                    ClearEncounter();
                    return;
                }

                var data = message.Data;
                var isLife = _life != 0 && opcode == _life;
                var isDespawn = _despawn != 0 && opcode == _despawn;
                if ((isLife && data.Count >= 23) || (isDespawn && data.Count >= 26))
                {
                    var id = BitConverter.ToUInt64(data.Array!, data.Offset + 2);
                    var state = isDespawn ? BitConverter.ToUInt32(data.Array!, data.Offset + 22) : data.Array![data.Offset + 22];
                    if (isLife && id == _self())
                    {
                        if (state != 0)
                        {
                            _awaitingBoss = false;
                            return;
                        }
                        var wasInCombat = GetCombatState();
                        // The observer sees each packet before the core consumes its queue.
                        _awaitingBoss = _awaitingBoss || (wasInCombat && !_activeBoss.HasValue);
                        _characterInCombat = false;
                        _changed(GetCombatState());
                        return;
                    }

                    // Asura's training boss reset removes the old actor with type 0.
                    // Type 1 is a visibility change, so the fight can still be active.
                    if (isLife ? state == 0 : state is 0 or 3 or 5)
                    {
                        GetCombatState();
                        if (id == (_activeBoss ?? _target()) && (id != _finishedBoss || _canRepull))
                            FinishBoss(id, isDespawn && state == 3);
                    }
                    return;
                }

                if (_occupier != 0 && opcode == _occupier && data.Count >= 26)
                {
                    var id = BitConverter.ToUInt64(data.Array!, data.Offset + 2);
                    if (id != _activeBoss && id != _target()) return;
                    var target = BitConverter.ToUInt64(data.Array!, data.Offset + 18);
                    if (target == 0)
                    {
                        // Match the core's SNpcOccupierInfo.HasReset condition.
                        GetCombatState();
                        if (_activeBoss == id) FinishBoss(id, canRepull: true);
                    }
                    else if (_finishedBoss != id || _canRepull)
                    {
                        _finishedBoss = null;
                        _awaitingBoss = false;
                        _activeBoss = id;
                        _changed(true);
                    }
                    return;
                }

                if (_status == 0 || opcode != _status || data.Count < 11) return;
                if (_self() != BitConverter.ToUInt64(data.Array!, data.Offset + 2)) return;
                GetCombatState();
                var inCombat = data.Array![data.Offset + 10] == 1;
                if (inCombat && !_characterInCombat) _suppressStance = false;
                _characterInCombat = inCombat;
                _changed(GetCombatState());
            }
        }
        catch { /* Optional visual status must never interrupt the meter's actual packet processing. */ }
    }
    private void ReadVersion(Message message)
    {
        var data = message.Data;
        if (data.Count < 6) return;
        var count = BitConverter.ToUInt16(data.Array!, data.Offset + 2);
        var offset = BitConverter.ToUInt16(data.Array!, data.Offset + 4);
        for (var i = 0; i < count; i++)
        {
            var index = offset - 2; // Game offsets include the length word omitted by the receiver.
            if (index < 6 || index + 12 > data.Count) return;
            if (BitConverter.ToUInt32(data.Array!, data.Offset + index + 4) == 0)
            {
                _version = BitConverter.ToUInt32(data.Array!, data.Offset + index + 8);
                _status = _load = _life = _despawn = _occupier = 0; TryConfigure(); return;
            }
            offset = BitConverter.ToUInt16(data.Array!, data.Offset + index + 2);
        }
    }
    public void Dispose()
    {
        _sniffer.MessageReceived -= Observe; _sniffer.EndConnection -= Reset; _sniffer.NewConnection -= OnConnection;
    }
}
