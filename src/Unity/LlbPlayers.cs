using System;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using NeonHud.Core;

namespace NeonHud
{
    internal sealed class LlbPlayers
    {
        private const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const double FixOne = 4294967296.0;

        private readonly ManualLogSource _log;
        private bool _bound, _broken;
        private float _nextBind, _retry = 2f;

        private FieldInfo _hudInstance, _infos, _onlyP0, _shownPlayer, _lbName, _playerEntity;
        private FieldInfo _playerData, _character, _hp, _energy, _stocks, _points, _team, _raw;
        private FieldInfo _settings, _hpFactor, _energyMax, _mode;
        private MethodInfo _pointsMode, _score;
        private FieldInfo _ballHandler, _worldState;
        private MethodInfo _getBall, _noBalls, _getDamage;

        private readonly object[] _scoreArgs = new object[3];
        private readonly object[] _ballArgs = { 0 };
        private readonly object[] _teamArg = new object[1];
        private readonly string[] _rawNames = new string[4], _cleanNames = new string[4];

        public LlbPlayers(ManualLogSource log) { _log = log; }

        public bool Read(PlayerCard[] cards, float now)
        {
            for (int i = 0; i < cards.Length; i++) cards[i].Present = false;
            if (!Bind(now)) return false;
            try
            {
                var hud = _hudInstance.GetValue(null) as UnityEngine.Object;
                if (hud == null) return true;
                var infos = _infos.GetValue(hud) as Array;
                if (infos == null) return true;
                bool onlyP0 = _onlyP0 != null && (bool)_onlyP0.GetValue(hud);

                object settings = _settings.GetValue(null);
                string factor = settings != null ? _hpFactor.GetValue(settings).ToString() : "HP_100";
                bool hpOff = factor == "OFF";
                int hpMax = HpMax(factor);
                int energyMax = settings != null ? (int)_energyMax.GetValue(settings) : 0;
                bool pointsMode = settings != null && _pointsMode != null && (bool)_pointsMode.Invoke(settings, null);
                object mode = _mode != null ? _mode.GetValue(null) : null;

                object ball = null;
                bool live = false;
                object handler = _ballHandler.GetValue(null);
                if (handler != null)
                {
                    live = Convert.ToInt32(_worldState.GetValue(null)) == 1 && !(bool)_noBalls.Invoke(handler, null);
                    if (live) ball = _getBall.Invoke(handler, _ballArgs);
                    if ((ball as UnityEngine.Object) == null) { ball = null; live = false; }
                }

                for (int i = 0; i < cards.Length && i < infos.Length; i++)
                {
                    if (onlyP0 && i > 0) continue;
                    var info = infos.GetValue(i) as UnityEngine.Object;
                    if (info == null) continue;
                    object player = _shownPlayer.GetValue(info);
                    if (player == null) continue;
                    var entity = _playerEntity.GetValue(player) as UnityEngine.Object;
                    if (entity == null) continue;
                    object data = _playerData.GetValue(entity);
                    if (data == null) continue;

                    PlayerCard card = cards[i];
                    object team = _team.GetValue(data);
                    long hpRaw = Raw(_hp.GetValue(data));

                    float threat = 0f;
                    bool lethal = false;
                    if (live && !hpOff)
                    {
                        _teamArg[0] = team;
                        long dmgRaw = Raw(_getDamage.Invoke(ball, _teamArg));
                        threat = (float)(dmgRaw / FixOne);
                        lethal = dmgRaw >= hpRaw;
                    }

                    card.Present = true;
                    card.SetIdentity(i, Convert.ToInt32(team), CharacterName(_character.GetValue(entity)), CleanName(i, info));
                    card.SetHp((float)(hpRaw / FixOne), hpMax, hpOff, threat, lethal, now);

                    int energy = (int)_energy.GetValue(data);
                    card.Energy01 = energyMax > 0 ? energy / (float)energyMax : 0f;
                    card.EnergyFull = energyMax > 0 && energy >= energyMax;

                    int stocks = (int)_stocks.GetValue(data), points = _points != null ? (int)_points.GetValue(data) : -1;
                    if (mode != null && _score != null)
                    {
                        _scoreArgs[0] = entity; _scoreArgs[1] = 0; _scoreArgs[2] = 0;
                        _score.Invoke(mode, _scoreArgs);
                        stocks = (int)_scoreArgs[1];
                        points = (int)_scoreArgs[2];
                    }
                    card.SetScore(pointsMode, stocks, points);
                }
                return true;
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                _broken = true;
                _log.LogWarning("Player cards: reading player data failed, cards disabled until config reload: " + inner.GetType().Name + ": " + inner.Message);
                for (int i = 0; i < cards.Length; i++) cards[i].Present = false;
                return false;
            }
        }

        private long Raw(object fixedPoint) { return (long)_raw.GetValue(fixedPoint); }

        private static int HpMax(string factor)
        {
            switch (factor)
            {
                case "HP_200": return 200;
                case "HP_175": return 175;
                case "HP_150": return 150;
                case "HP_125": return 125;
                case "HP_75": return 75;
                case "HP_50": return 50;
                default: return 100;
            }
        }

        private static string CharacterName(object character)
        {
            string c = character != null ? character.ToString() : "";
            switch (c)
            {
                case "KID": return "RAPTOR";
                case "ROBOT": return "SWITCH";
                case "CANDY": return "CANDYMAN";
                case "BOOM": return "SONATA";
                case "CROC": return "LATCH";
                case "PONG": return "DICE";
                case "BOSS": return "DOOMBOX";
                case "COP": return "NITRO";
                case "ELECTRO": return "GRID";
                case "SKATE": return "JET";
                case "GRAF": return "TOXIC";
                case "BAG": return "DUST & ASHES";
                default: return c;
            }
        }

        private string CleanName(int i, object info)
        {
            string raw = null;
            var label = _lbName != null ? _lbName.GetValue(info) : null;
            if (label != null)
            {
                PropertyInfo p; FieldInfo f;
                if (TextAccess.Find(label.GetType(), out p, out f))
                    raw = (p != null ? p.GetValue(label, null) : f.GetValue(label)) as string;
            }
            if (raw == _rawNames[i]) return _cleanNames[i];
            _rawNames[i] = raw;
            if (raw == null) return _cleanNames[i] = "";
            var sb = new StringBuilder(raw.Length);
            bool tag = false;
            foreach (char ch in raw)
            {
                if (ch == '<') { tag = true; continue; }
                if (tag) { if (ch == '>') tag = false; continue; }
                if (ch != '\n' && ch != '\r') sb.Append(ch);
            }
            return _cleanNames[i] = sb.ToString().Trim().ToUpperInvariant();
        }

        private bool Bind(float now)
        {
            if (_bound) return true;
            if (_broken || now < _nextBind) return false;

            Type hud = ReflectionPath.FindType("LLScreen.ScreenGameHud");
            if (hud == null) { _nextBind = now + _retry; _retry = Math.Min(30f, _retry * 2f); return false; }

            _hudInstance = hud.GetField("instance", S);
            _infos = hud.GetField("playerInfos", I);
            _onlyP0 = hud.GetField("onlyShowPlayerInfo0", I);
            Type infoType = _infos != null ? _infos.FieldType.GetElementType() : null;
            _shownPlayer = infoType != null ? infoType.GetField("shownPlayer", I) : null;
            _lbName = infoType != null ? infoType.GetField("lbName", I) : null;

            Type entityType = ReflectionPath.FindType("GameplayEntities.PlayerEntity");
            _playerEntity = _shownPlayer != null && entityType != null ? FieldOfType(_shownPlayer.FieldType, entityType, "JCCIAMJEODH") : null;
            _playerData = entityType != null ? Find(entityType, "playerData", I) : null;
            _character = entityType != null ? Find(entityType, "character", I) : null;
            Type dataType = _playerData != null ? _playerData.FieldType : null;
            _hp = dataType != null ? Find(dataType, "hp", I) : null;
            _energy = dataType != null ? Find(dataType, "energy", I) : null;
            _stocks = dataType != null ? Find(dataType, "stocks", I) : null;
            _points = dataType != null ? Find(dataType, "points", I) : null;
            _team = dataType != null ? Find(dataType, "team", I) : null;
            _raw = _hp != null ? RawField(_hp.FieldType) : null;

            Type settings = ReflectionPath.FindType("JOMBNFKIHIC");
            _settings = settings != null ? settings.GetField("GIGAKBJGFDI", S) : null;
            _hpFactor = settings != null ? settings.GetField("BKKMIBGLAEC", I) : null;
            _energyMax = settings != null ? settings.GetField("IKGAIFLLLAG", I) : null;
            _pointsMode = settings != null ? settings.GetMethod("JFNLOKKDCEB", I, null, Type.EmptyTypes, null) : null;

            Type mode = ReflectionPath.FindType("NCMFHODLNAJ");
            _mode = mode != null ? mode.GetField("GIGAKBJGFDI", S) : null;
            _score = mode != null && entityType != null
                ? mode.GetMethod("BPDKCCGOHPO", I, null, new[] { entityType, typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }, null)
                : null;
            if (_mode == null) _score = null;

            Type balls = ReflectionPath.FindType("LLHandlers.BallHandler");
            _ballHandler = balls != null ? balls.GetField("instance", S) : null;
            _getBall = balls != null ? balls.GetMethod("GetBall", I, null, new[] { typeof(int) }, null) : null;
            _noBalls = balls != null ? balls.GetMethod("NoBallsActivelyInMatch", I, null, Type.EmptyTypes, null) : null;
            Type ballType = ReflectionPath.FindType("GameplayEntities.GetHitBallEntity");
            _getDamage = ballType != null && _team != null ? ballType.GetMethod("GetDamage", I, null, new[] { _team.FieldType }, null) : null;
            Type world = ReflectionPath.FindType("World");
            _worldState = world != null ? world.GetField("state", S) : null;

            string missing =
                  _hudInstance == null ? "ScreenGameHud.instance"
                : _infos == null ? "ScreenGameHud.playerInfos"
                : _shownPlayer == null ? "GameHudPlayerInfo.shownPlayer"
                : _playerEntity == null ? "Player -> PlayerEntity"
                : _playerData == null || _hp == null || _energy == null || _stocks == null || _team == null ? "PlayerData fields"
                : _character == null ? "PlayerEntity.character"
                : _raw == null ? "fixed-point raw value"
                : _settings == null || _hpFactor == null || _energyMax == null ? "game settings (HP factor / max energy)"
                : _ballHandler == null || _getBall == null || _noBalls == null || _getDamage == null || _worldState == null ? "ball damage"
                : null;
            if (missing != null)
            {
                _broken = true;
                _log.LogWarning("Player cards: couldn't find " + missing + " in this game version; cards disabled.");
                return false;
            }
            if (_score == null) _log.LogInfo("Player cards: game-mode score lookup not found, using stocks/points fields directly.");
            _bound = true;
            _log.LogInfo("Player cards linked to the game's player data.");
            return true;
        }

        private static FieldInfo Find(Type t, string name, BindingFlags flags)
        {
            for (; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        private static FieldInfo FieldOfType(Type owner, Type fieldType, string preferred)
        {
            FieldInfo named = owner.GetField(preferred, I);
            if (named != null && fieldType.IsAssignableFrom(named.FieldType)) return named;
            FieldInfo found = null;
            foreach (FieldInfo f in owner.GetFields(I))
            {
                if (!fieldType.IsAssignableFrom(f.FieldType)) continue;
                if (found != null) return null;
                found = f;
            }
            return found;
        }

        private static FieldInfo RawField(Type fixedType)
        {
            FieldInfo found = null;
            foreach (FieldInfo f in fixedType.GetFields(I))
            {
                if (f.FieldType != typeof(long)) continue;
                if (found != null) return null;
                found = f;
            }
            return found;
        }
    }
}
