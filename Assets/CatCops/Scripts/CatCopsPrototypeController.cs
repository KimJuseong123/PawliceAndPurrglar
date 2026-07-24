using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatCops
{
    public sealed class CatCopsPrototypeController : MonoBehaviour
    {
        [Header("Actors")]
        public CatCopsActor Police;
        public CatCopsActor Thief;
        public CatCopsActor Dog;
        public CatCopsActor Cat;
        public CatCopsActor Merchant;

        [Header("Map Points")]
        public Transform PoliceStart;
        public Transform ThiefStart;
        public Transform TreasureShop;
        public Transform BlackMarket;
        public Transform CatRooftop;
        public Transform[] ThiefEscapeRoute;

        [Header("Prefabs")]
        public GameObject StoneProjectilePrefab;
        public GameObject BoneProjectilePrefab;
        public GameObject StarBurstPrefab;

        [Header("UI")]
        public Text TimerText;
        public Text PoliceScoreText;
        public Text ThiefScoreText;
        public Text DogStatusText;
        public Text CatStatusText;
        public Text CommandStatusText;
        public Text RoundStatusText;

        [Header("Rules")]
        public float RoundDuration = 155f;
        public float CaptureDistance = 1.15f;
        public float ThrowCooldown = 0.8f;
        public float StunDuration = 1.5f;
        public float ConfuseDuration = 3f;
        public int BlackMarketSaleValue = 120;

        private readonly Dictionary<CatCopsActorRole, CatCopsActor> _actors = new Dictionary<CatCopsActorRole, CatCopsActor>();
        private CatCopsTopDownEngineBridge _bridge;
        private float _timer;
        private float _throwTimer;
        private float _thiefStunTimer;
        private float _dogConfuseTimer;
        private float _catHideTimer;
        private int _routeIndex;
        private int _policeScore;
        private int _thiefMoney;
        private bool _dogChasing;
        private bool _roundEnded;

        private void Awake()
        {
            _bridge = GetComponent<CatCopsTopDownEngineBridge>();
            Register(Police);
            Register(Thief);
            Register(Dog);
            Register(Cat);
            Register(Merchant);
        }

        private void Start()
        {
            ResetRound();
            _bridge?.SignalMatchStart();
        }

        private void Update()
        {
            if (_roundEnded)
            {
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    ResetRound();
                }
                return;
            }

            float dt = Time.deltaTime;
            _timer -= dt;
            _throwTimer -= dt;
            TickStatusTimers(dt);
            HandlePlayerInput(dt);
            TickThief(dt);
            TickDog(dt);
            TickCat(dt);
            CheckCapture();
            UpdateHud();

            if (_timer <= 0f)
            {
                EndRound(_policeScore > 0 && _thiefMoney < BlackMarketSaleValue * 2);
            }
        }

        public CatCopsActor GetActor(CatCopsActorRole role)
        {
            return _actors.TryGetValue(role, out CatCopsActor actor) ? actor : null;
        }

        public void ResolveProjectileHit(CatCopsActor target, string itemId)
        {
            if (target == null)
            {
                return;
            }

            if (target.Role == CatCopsActorRole.Thief)
            {
                _thiefStunTimer = StunDuration;
                target.SetStatus(true, false);
                SetStatus("명중! 도둑이 잠시 기절했습니다.");
                SpawnBurst(target.transform.position);
                _bridge?.SignalScore(10);
            }
            else if (target.Role == CatCopsActorRole.Dog)
            {
                _dogConfuseTimer = ConfuseDuration;
                target.SetStatus(false, true);
                _dogChasing = false;
                SetStatus("강아지가 뼈다귀에 정신이 팔렸습니다.");
                SpawnBurst(target.transform.position);
            }
            else if (target.Role == CatCopsActorRole.Cat)
            {
                _catHideTimer = StunDuration;
                target.SetStatus(true, false);
                SetStatus("고양이가 깜짝 놀라 멈췄습니다.");
                SpawnBurst(target.transform.position);
            }
        }

        private void Register(CatCopsActor actor)
        {
            if (actor != null)
            {
                _actors[actor.Role] = actor;
            }
        }

        private void ResetRound()
        {
            _timer = RoundDuration;
            _throwTimer = 0f;
            _thiefStunTimer = 0f;
            _dogConfuseTimer = 0f;
            _catHideTimer = 0f;
            _routeIndex = 0;
            _policeScore = 0;
            _thiefMoney = 0;
            _dogChasing = true;
            _roundEnded = false;

            Snap(Police, PoliceStart);
            Snap(Thief, ThiefStart);
            Snap(Dog, PoliceStart, new Vector3(-1.25f, 0f, -0.8f));
            Snap(Cat, CatRooftop);
            if (Thief != null)
            {
                Thief.HasLoot = true;
                Thief.SetStatus(false, false);
            }
            if (Dog != null)
            {
                Dog.SetStatus(false, false);
            }
            if (Cat != null)
            {
                Cat.SetStatus(false, false);
            }

            SetStatus("강아지 추적 시작. R로 던지고 1/2/3으로 음성 명령을 시뮬레이션하세요.");
            UpdateHud();
        }

        private static void Snap(CatCopsActor actor, Transform point)
        {
            Snap(actor, point, Vector3.zero);
        }

        private static void Snap(CatCopsActor actor, Transform point, Vector3 offset)
        {
            if (actor == null || point == null)
            {
                return;
            }

            actor.transform.position = point.position + offset;
            actor.transform.rotation = point.rotation;
        }

        private void TickStatusTimers(float dt)
        {
            if (_thiefStunTimer > 0f)
            {
                _thiefStunTimer -= dt;
                if (_thiefStunTimer <= 0f && Thief != null)
                {
                    Thief.SetStatus(false, false);
                    SetStatus("도둑이 다시 달리기 시작합니다.");
                }
            }

            if (_dogConfuseTimer > 0f)
            {
                _dogConfuseTimer -= dt;
                if (_dogConfuseTimer <= 0f && Dog != null)
                {
                    Dog.SetStatus(false, false);
                    _dogChasing = true;
                    SetStatus("강아지가 다시 냄새를 따라갑니다.");
                }
            }

            if (_catHideTimer > 0f)
            {
                _catHideTimer -= dt;
                if (_catHideTimer <= 0f && Cat != null)
                {
                    Cat.SetStatus(false, false);
                }
            }
        }

        private void HandlePlayerInput(float dt)
        {
            if (Police == null)
            {
                return;
            }

            Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            if (move.sqrMagnitude > 0.001f)
            {
                Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
                Vector3 worldMove = right * move.x + forward * move.z;
                Police.transform.position += worldMove * Police.MoveSpeed * dt;
                Police.transform.rotation = Quaternion.Slerp(
                    Police.transform.rotation,
                    Quaternion.LookRotation(worldMove, Vector3.up),
                    12f * dt);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ThrowAtThief();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.V))
            {
                IssueVoiceCommand("강아지, 저 도둑 쫓아!");
                _dogChasing = true;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                IssueVoiceCommand("고양이, 지붕으로 숨어!");
                _catHideTimer = ConfuseDuration;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                IssueVoiceCommand("가방 열어!");
                if (Cat != null && Cat.DistanceTo(BlackMarket) < 4f)
                {
                    _thiefMoney += 60;
                    SpawnBurst(Cat.transform.position);
                    SetStatus("고양이가 몰래 전달한 보석이 암시장에 팔렸습니다.");
                }
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                ThrowBoneAtDog();
            }
        }

        private void IssueVoiceCommand(string command)
        {
            SetStatus("음성 명령: " + command);
        }

        private void TickThief(float dt)
        {
            if (Thief == null || Thief.IsStunned)
            {
                return;
            }

            Transform target = GetCurrentRouteTarget();
            if (target == null)
            {
                return;
            }

            Vector3 destination = target.position;
            if (_dogChasing && Dog != null && Thief.DistanceTo(Dog.transform) < 4.5f)
            {
                Vector3 flee = Thief.transform.position - Dog.transform.position;
                flee.y = 0f;
                destination = Thief.transform.position + flee.normalized * 3f;
            }

            Thief.MoveTowards(destination, dt);
            if (Vector3.Distance(Flatten(Thief.transform.position), Flatten(target.position)) < 1.1f)
            {
                _routeIndex++;
                if (_routeIndex >= ThiefEscapeRoute.Length)
                {
                    SellLoot();
                }
            }
        }

        private Transform GetCurrentRouteTarget()
        {
            if (ThiefEscapeRoute == null || ThiefEscapeRoute.Length == 0)
            {
                return BlackMarket;
            }

            return ThiefEscapeRoute[Mathf.Clamp(_routeIndex, 0, ThiefEscapeRoute.Length - 1)];
        }

        private void TickDog(float dt)
        {
            if (Dog == null || Dog.IsConfused)
            {
                return;
            }

            Transform target = _dogChasing && Thief != null ? Thief.transform : Police != null ? Police.transform : null;
            if (target != null)
            {
                Dog.MoveTowards(target.position, dt);
            }
        }

        private void TickCat(float dt)
        {
            if (Cat == null || Cat.IsStunned)
            {
                return;
            }

            Transform target = _catHideTimer > 0f ? CatRooftop : BlackMarket;
            if (target != null)
            {
                Cat.MoveTowards(target.position, dt * 0.7f);
            }
        }

        private void CheckCapture()
        {
            if (Thief == null)
            {
                return;
            }

            bool policeClose = Police != null && Thief.DistanceTo(Police.transform) <= CaptureDistance;
            bool dogClose = Dog != null && !Dog.IsConfused && Thief.DistanceTo(Dog.transform) <= CaptureDistance;
            if (policeClose || dogClose)
            {
                _policeScore++;
                _bridge?.SignalScore(50);
                SpawnBurst(Thief.transform.position);
                SetStatus(policeClose ? "경찰이 도둑을 체포했습니다!" : "강아지가 도둑의 길을 막았습니다!");
                ResetThiefRun();
            }
        }

        private void ResetThiefRun()
        {
            _routeIndex = 0;
            _thiefStunTimer = 0f;
            Snap(Thief, ThiefStart);
            if (Thief != null)
            {
                Thief.SetStatus(false, false);
                Thief.HasLoot = true;
            }
        }

        private void SellLoot()
        {
            _thiefMoney += BlackMarketSaleValue;
            SpawnBurst(BlackMarket.position);
            SetStatus("도둑이 암시장에 전리품을 팔았습니다.");
            ResetThiefRun();
        }

        private void ThrowAtThief()
        {
            if (_throwTimer > 0f || Police == null || Thief == null || StoneProjectilePrefab == null)
            {
                return;
            }

            _throwTimer = ThrowCooldown;
            Vector3 origin = Police.transform.position + Vector3.up * 0.85f;
            Vector3 direction = Thief.transform.position - Police.transform.position;
            direction.y = 0f;
            GameObject projectile = Instantiate(StoneProjectilePrefab, origin, Quaternion.identity);
            projectile.GetComponent<CatCopsProjectile>()?.Launch(direction, this, CatCopsActorRole.Thief, "stone");
            SetStatus("던지기: 도둑을 조준합니다.");
        }

        private void ThrowBoneAtDog()
        {
            if (_throwTimer > 0f || Thief == null || Dog == null || BoneProjectilePrefab == null)
            {
                return;
            }

            _throwTimer = ThrowCooldown;
            Vector3 origin = Thief.transform.position + Vector3.up * 0.85f;
            Vector3 direction = Dog.transform.position - Thief.transform.position;
            direction.y = 0f;
            GameObject projectile = Instantiate(BoneProjectilePrefab, origin, Quaternion.identity);
            projectile.GetComponent<CatCopsProjectile>()?.Launch(direction, this, CatCopsActorRole.Dog, "bone");
            SetStatus("도둑이 뼈다귀를 던져 강아지를 흔듭니다.");
        }

        private void SpawnBurst(Vector3 position)
        {
            if (StarBurstPrefab != null)
            {
                Destroy(Instantiate(StarBurstPrefab, position + Vector3.up * 1.6f, Quaternion.identity), 1.25f);
            }
        }

        private void EndRound(bool policeWon)
        {
            _roundEnded = true;
            SetStatus(policeWon ? "경찰 승리! Enter로 재시작" : "도둑 승리! Enter로 재시작");
            _bridge?.SignalMatchEnd(policeWon);
        }

        private void SetStatus(string status)
        {
            if (CommandStatusText != null)
            {
                CommandStatusText.text = status;
            }
        }

        private void UpdateHud()
        {
            if (TimerText != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(_timer));
                TimerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }

            if (PoliceScoreText != null)
            {
                PoliceScoreText.text = $"경찰 {_policeScore}";
            }

            if (ThiefScoreText != null)
            {
                ThiefScoreText.text = $"도둑 ${_thiefMoney}";
            }

            if (DogStatusText != null)
            {
                DogStatusText.text = Dog != null && Dog.IsConfused ? $"강아지 혼란 {_dogConfuseTimer:0.0}s" : "강아지 추적 중";
            }

            if (CatStatusText != null)
            {
                CatStatusText.text = Cat != null && Cat.IsStunned ? $"고양이 멈춤 {_catHideTimer:0.0}s" : "고양이 은신/배달";
            }

            if (RoundStatusText != null)
            {
                RoundStatusText.text = "WASD 이동 | R 던지기 | 1/V 강아지 추적 | 2 고양이 은신 | 3 가방 열기 | B 뼈다귀 테스트";
            }
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
