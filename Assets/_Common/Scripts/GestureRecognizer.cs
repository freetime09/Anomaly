using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스로 그린 궤적을 캐릭터 명령으로 변환한다. (하이브리드)
///  1) $1 템플릿 인식 우선 — §, ★ 같은 복잡한 모양 (지금은 인식 시 콘솔 로그만).
///  2) 매칭이 없으면 기존 방향 시퀀스로 폴백 — [오른쪽]=오른쪽이동, [왼쪽]=왼쪽이동, [위]=점프, [아래,오른쪽]=오른쪽 슬라이드(ㄴ), [아래,왼쪽]=왼쪽 슬라이드(반대 ㄴ).
///
/// 녹화: F9 → recordSequence 모양들을 recordsPerShape 번씩 차례로 그리면 공용 녹화 파일에 추가된다.
///   (녹화 중 Backspace = 직전 것 지우기, F9 = 지금까지 그린 것만 저장하고 끝)
///
/// 템플릿은 공용 녹화 파일(Resources/GestureRecordings, 패키지·빌드에 자동 포함) + 이 PC 사본에서 불러온다.
/// F10 = 불러온 템플릿 확인 창.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class GestureRecognizer : MonoBehaviour
{
    public enum Dir { None, Up, Down, Left, Right }

    [Header("인식 튜닝")]
    [Tooltip("이 거리(px)보다 가까운 점은 버려 손 떨림을 제거")]
    public float minGap = 5f;
    [Tooltip("꺾인 획(ㄴ·ㄱ)에서 이 길이(px)보다 짧은 방향 조각은 손떨림으로 보고 버림 (절대 최소값)")]
    public float minSegmentLength = 25f;
    [Tooltip("꺾인 획(ㄴ·ㄱ)에서 전체 획 길이의 이 비율보다 짧은 조각도 버림. 작게 그려도, 모서리가 뭉개져도 인식되게")]
    [Range(0f, 0.5f)]
    public float minSegmentRatio = 0.2f;
    [Tooltip("곧은 획(이동/점프)으로 인정할 최소 길이(px). 이보다 짧으면 실수로 끈 것으로 보고 무시")]
    public float minStraightLength = 30f;
    [Tooltip("꺾인 획에서 짧은 조각을 버리고 남은 조각들이 전체 획 길이의 이 비율 이상일 때만 방향 명령으로 인정. 낮으면 망가진 모양(예: 대충 그린 N)이 엉뚱한 이동/점프로 발동")]
    [Range(0f, 1f)]
    public float minSequenceCoverage = 0.65f;
    [Tooltip("템플릿·방향 조각으로 모두 인식 실패했을 때, 곧은 정도가 이 값 이상이면 시작→끝 방향으로 이동/점프 판정(아치형·물결 모양 이동 구제). 지그재그·V 등은 이보다 낮아 계속 실패")]
    [Range(0f, 1f)]
    public float looseStraightness = 0.7f;

    [Header("ㄱ 공격 판정 (모서리 기준 두 획)")]
    [Tooltip("ㄱ의 가로획이 수평에서 이 각도(도)까지 기울어도 인정")]
    public float attackHorizontalTol = 40f;
    [Tooltip("ㄱ의 세로획이 안쪽(가로획 시작 쪽, 손글씨 ㄱ처럼 왼쪽 아래)으로 기울어도 되는 최대 각도(도)")]
    public float attackLeanIn = 55f;
    [Tooltip("ㄱ의 세로획이 바깥쪽으로 기울어도 되는 최대 각도(도). 크면 아치형 이동이 ㄱ으로 오인식됨")]
    public float attackLeanOut = 35f;
    [Tooltip("짧은 획이 긴 획의 이 비율 이상이어야 ㄱ으로 인정 (끝 삐침 배제)")]
    [Range(0f, 1f)]
    public float attackMinLegRatio = 0.3f;
    [Tooltip("각 획의 곧은 정도(직선거리/경로) 최소값. 낮을수록 구불구불해도 인정")]
    [Range(0.1f, 1f)]
    public float attackLegStraightness = 0.75f;
    [Tooltip("ㄱ/S 구분: 좌우 방향이 이 거리(px) 이상 되돌아가야 '방향 바뀜'으로 셈. 2번 이상 바뀌면 ㄱ 공격이 아님")]
    public float reversalTolerance = 20f;
    [Tooltip("ㄱ으로 오인될 뻔한 지그재그 획은 S 템플릿 점수가 이 이상이면 S(돌아오기)로 처리")]
    public float sFallbackThreshold = 0.65f;

    [Header("$1 템플릿 인식")]
    [Tooltip("이 점수 이상이면 템플릿 제스처로 인정 (0~1). 높을수록 엄격")]
    public float recognizeThreshold = 0.84f;
    [Tooltip("이 값보다 '곧은'(직선에 가까운) 획은 템플릿 인식을 건너뛰고 방향(이동/점프)으로만 처리. 직선이 S·O 등 곡선 템플릿으로 오인식되는 것 방지. 1=완전 직선")]
    public float straightLineBypass = 0.9f;
    [Tooltip("번들 기본 템플릿 에셋(json). 지정하면 이걸로 로드 → 패키지·빌드에 자동 포함(누락 없음). 비우면 StreamingAssets 경로에서 읽음(구버전 호환)")]
    public TextAsset bundledTemplates;

    [Header("전체 녹화 (F9) — 모든 모양을 순서대로 다시 녹화해 번들 템플릿을 새로 만든다")]
    [Tooltip("녹화할 모양 이름과 순서")]
    public string[] recordSequence = { "O(상호작용)", "N", "9", "S", "⋉" };
    [Tooltip("모양마다 몇 번씩 그릴지 (모두 같은 개수 → 한 모양으로 쏠리지 않음)")]
    public int recordsPerShape = 4;

    [Header("제스처별 궤적 색 (기본 이동/점프는 흰색 유지)")]
    [Tooltip("N 나뭇가지 던지기")]  public Color colorThrow    = new Color(1f, 0.55f, 0.1f);   // 주황
    [Tooltip("9 나뭇가지 세우기")]  public Color colorPlant    = new Color(0.35f, 0.9f, 0.35f); // 초록
    [Tooltip("S 나뭇가지 돌아오기")] public Color colorReturn   = new Color(0.3f, 0.7f, 1f);     // 하늘
    [Tooltip("⋉ 텔레포트")]         public Color colorTeleport = new Color(0.75f, 0.4f, 1f);    // 보라
    [Tooltip("O 상호작용")]         public Color colorInteract = new Color(1f, 0.9f, 0.25f);    // 노랑
    [Tooltip("ㄴ 슬라이드(좌/우)")] public Color colorSlide    = new Color(0.2f, 0.9f, 0.85f);  // 청록
    [Tooltip("ㄱ 공격")]            public Color colorAttack   = new Color(1f, 0.35f, 0.35f);   // 빨강
    [Tooltip("§ 보스")]             public Color colorBoss     = new Color(0.9f, 0.15f, 0.15f); // 진빨강
    [Tooltip("그 외 인식된 템플릿(위에 없는 모양)")] public Color colorOther = new Color(0.8f, 0.8f, 0.85f);

    [Header("사운드")]
    [Tooltip("제스처 인식 실패 시 재생할 소리")]
    public AudioClip failClip;
    [Range(0f, 1f)]
    [Tooltip("실패 소리 볼륨")]
    public float failVolume = 1f;
    [Tooltip("마우스로 그리는 동안 계속 재생할 소리(루프). 손 떼면 멈춤")]
    public AudioClip drawLoopClip;
    [Range(0f, 1f)]
    [Tooltip("그리기 소리 볼륨")]
    public float drawVolume = 1f;

    [Header("제스처별 인식음 (모양마다 다른 소리, 비우면 안 남)")]
    [Tooltip("인식음 볼륨")]
    [Range(0f, 1f)]
    public float recognizeVolume = 1f;
    [Tooltip("N 나뭇가지 던지기 — ⚠ 이미 throwClip 있음(겹침 주의)")]      public AudioClip sfxThrow;
    [Tooltip("O 상호작용")]                                              public AudioClip sfxInteract;
    [Tooltip("9 나뭇가지 세우기")]                                        public AudioClip sfxPlant;
    [Tooltip("S 나뭇가지 돌아오기")]                                      public AudioClip sfxReturn;
    [Tooltip("⋉ 텔레포트 — ⚠ 이미 branchTeleportClip 있음(겹침 주의)")]  public AudioClip sfxTeleport;
    [Tooltip("§ 보스")]                                                  public AudioClip sfxBoss;
    [Tooltip("ㄱ 공격 — ⚠ 이미 attackClip 있음(겹침 주의)")]             public AudioClip sfxAttack;
    [Tooltip("ㄴ 슬라이드(좌/우) — ⚠ 이미 slideClip 있음(겹침 주의)")]   public AudioClip sfxSlide;
    [Tooltip("좌우 이동")]                                               public AudioClip sfxMove;
    [Tooltip("점프")]                                                    public AudioClip sfxJump;
    [Tooltip("그 외 인식된 템플릿(위에 없는 모양)")]                       public AudioClip sfxOther;

    PlayerController player;
    AudioSource sfx;     // 실패음 등 단발 재생용
    AudioSource drawSfx; // 그리는 동안 루프 재생용
    GestureTrail trail;  // 인식된 모양 색을 입힐 궤적 (씬의 GestureTrail)
    readonly List<Vector2> points = new List<Vector2>();
    Vector2 origin;  // 그린 시작점 = 원점(0,0)
    bool drawing;
    /// <summary>지금 마우스로 제스처를 그리는 중인가 (나뭇가지 세울 자리 미리보기 표시용)</summary>
    public bool IsDrawing => drawing;

    /// <summary>그리는 중인 획이 지금 '가로 직선'(곧음 ≥ straightLineBypass, 시작→현재 점이 수평 ±45°)이면 true.
    /// 손을 떼면 직선 이동으로 처리될 획일 때만 켜진다 (ㄴ·아치 등 다른 모양 중엔 안 뜸).
    /// 이동 도착 지점 미리보기용: dir = 방향, px = 지금까지 그은 가로 폭 (실제 이동 거리 계산과 동일).</summary>
    public bool TryGetMovePreview(out int dir, out float px)
    {
        dir = 0; px = 0f;
        if (!drawing || points.Count < 2) return false;
        Vector2 v = points[points.Count - 1] - points[0];
        if (v.magnitude < minStraightLength || Mathf.Abs(v.y) > Mathf.Abs(v.x)) return false;
        if (StrokeStraightness() < straightLineBypass) return false; // 꺾이거나 휜 획 → 미리보기 없음
        dir = v.x > 0f ? 1 : -1;
        px = HorizontalSpanPx();
        return true;
    }

    readonly List<UnistrokeRecognizer.Template> templates = new List<UnistrokeRecognizer.Template>();
    bool fullRecording;   // F9 전체 녹화 중
    bool showCheck;       // F10 템플릿 확인 창
    int sharedCount;      // F10 확인용: 공용 파일에서 읽은 개수
    int myCopyCount;      // F10 확인용: 이 PC 사본 개수
    int restoredCount;    // F10 확인용: 공용 파일에서 빠져 있어 사본에서 되살린 개수

    /// <summary>공용 녹화 파일 — 모든 사람 녹화가 여기 하나에 쌓인다 (Resources 아래라 패키지·빌드에 자동 포함)</summary>
    const string SharedResourceName = "GestureRecordings/gesture_templates";
    const string SharedFilePath = "Assets/_Common/GestureData/Resources/GestureRecordings/gesture_templates.json";
    /// <summary>이 PC에서 F9로 녹화한 것의 사본 — 패키지를 받아 공용 파일이 덮어써져도 여기서 다시 채워 넣는다</summary>
    string MyCopyPath => System.IO.Path.Combine(Application.persistentDataPath, "gesture_my_recordings.json");
    int bundledVersion = -1; // 번들 템플릿 파일 형식 버전 (-1 = 없음/못 읽음)
    int recIndex;         // 전체 녹화: 지금 녹화 중인 모양 순서
    readonly List<UnistrokeRecognizer.Template> recNew = new List<UnistrokeRecognizer.Template>();
    // 번들 기본값 (프로젝트에 포함 → 패키지·빌드에 자동 포함)
    string BundledTemplatePath => System.IO.Path.Combine(Application.streamingAssetsPath, "gesture_templates.json");

    void Awake()
    {
        player = GetComponent<PlayerController>();
        trail = FindObjectOfType<GestureTrail>();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        drawSfx = gameObject.AddComponent<AudioSource>();
        drawSfx.playOnAwake = false;
        drawSfx.loop = true; // 그리는 동안 이어짐
        LoadTemplates();
    }

    /// <summary>인식된 모양의 색으로 (사라지는) 궤적을 물들인다. 궤적이 없으면 무시.</summary>
    void FlashTrail(Color c) { if (trail != null) trail.SetStrokeColor(c); }

    /// <summary>모양별 인식음 재생 (비어 있으면 무음). 색 칠하는 지점마다 같이 호출.</summary>
    void PlayGesture(AudioClip clip) { if (clip != null && sfx != null) sfx.PlayOneShot(clip, recognizeVolume); }

    /// <summary>템플릿 이름 → 인식음. 표에 없는 이름은 sfxOther.</summary>
    AudioClip GestureClip(string name)
    {
        switch (name)
        {
            case "N": return sfxThrow;
            case "O(상호작용)": return sfxInteract;
            case "9": return sfxPlant;
            case "S": return sfxReturn;
            case "⋉": return sfxTeleport;
            case "§": return sfxBoss;
            default: return sfxOther;
        }
    }

    /// <summary>템플릿 이름 → 궤적 색. 표에 없는 이름은 colorOther.</summary>
    Color TemplateColor(string name)
    {
        switch (name)
        {
            case "N": return colorThrow;
            case "O(상호작용)": return colorInteract;
            case "9": return colorPlant;
            case "S": return colorReturn;
            case "⋉": return colorTeleport;
            case "§": return colorBoss;
            default: return colorOther;
        }
    }

    void Update()
    {
        // 설정 메뉴가 열려 있으면 그리기 막음 (그리던 획은 취소)
        if (PauseMenu.IsPaused)
        {
            CancelStroke();
            return;
        }

        // 그리는 중 우클릭 → 이번 획 취소 (명령 실행 안 함, 왼쪽 버튼을 뗄 때까지 다시 그려지지 않음)
        if (drawing && Input.GetMouseButtonDown(1))
        {
            CancelStroke();
            return;
        }

        // 녹화 조작
        if (Input.GetKeyDown(KeyCode.F10)) showCheck = !showCheck; // 템플릿 확인 창 열기/닫기
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (fullRecording)
            {
                // 중간에 F9 → 지금까지 그린 것만 저장하고 끝 (하나도 없으면 그냥 그만둠)
                if (recNew.Count > 0) FinishFullRecording();
                else { fullRecording = false; Debug.Log("[녹화] 전체 녹화 그만둠 — 그린 게 없어 저장 안 함."); }
            }
            else StartFullRecording();
        }
        if (fullRecording && Input.GetKeyDown(KeyCode.Backspace)) UndoFullRecording();

        // 그리기
        if (Input.GetMouseButtonDown(0))
        {
            if (!PauseMenu.IsPointerOnSettingsButton(Input.mousePosition)) BeginStroke(); // 설정 아이콘 클릭은 그리기 아님
        }
        else if (Input.GetMouseButton(0) && drawing) AddPoint();
        else if (Input.GetMouseButtonUp(0) && drawing) EndStroke();
    }

    /// <summary>그리던 획을 버림 (인식/명령 없음).</summary>
    void CancelStroke()
    {
        if (!drawing) return;
        drawing = false;
        points.Clear();
        if (drawSfx != null && drawSfx.isPlaying) drawSfx.Stop();
    }

    void BeginStroke()
    {
        drawing = true;
        origin = Input.mousePosition; // 시작점을 원점으로
        points.Clear();
        points.Add(Vector2.zero);     // 원점 (0,0)
        // 그리는 동안 소리 시작 (손 떼면 EndStroke 에서 멈춤)
        if (drawLoopClip != null && drawSfx != null)
        {
            drawSfx.clip = drawLoopClip;
            drawSfx.volume = drawVolume;
            if (!drawSfx.isPlaying) drawSfx.Play();
        }
    }

    void AddPoint()
    {
        Vector2 rel = (Vector2)Input.mousePosition - origin; // 시작점 기준 상대 좌표
        if (Vector2.Distance(rel, points[points.Count - 1]) >= minGap)
            points.Add(rel);
    }

    void EndStroke()
    {
        drawing = false;
        if (drawSfx != null && drawSfx.isPlaying) drawSfx.Stop(); // 손 떼면 그리기 소리 멈춤

        // 전체 녹화 중이면: 이번 획을 지금 순서의 모양으로 저장하고 끝 (명령 실행 안 함)
        if (fullRecording)
        {
            RecordForSequence();
            return;
        }

        // 0) 거의 '직선'인 획은 곡선형 템플릿일 리 없으니 템플릿을 건너뛰고, 시작→끝 전체 방향으로만 판정한다
        //    (S·O 오인식 방지 + 비스듬하거나 떨리거나 끝이 삐친 직선도 이동/점프로 인정)
        if (StrokeStraightness() >= straightLineBypass)
        {
            HandleStraightStroke();
            return;
        }

        // 1) $1 템플릿 인식 우선
        var (name, score) = UnistrokeRecognizer.Recognize(points, templates);
        if (name != null && score >= recognizeThreshold)
        {
            Debug.Log($"[제스처] '{name}' 인식됨 (점수 {score:F2})");
            FlashTrail(TemplateColor(name)); // 인식된 모양 색으로 궤적 물들임
            PlayGesture(GestureClip(name)); // 모양별 인식음
            switch (name)
            {
                case "N": // 나뭇가지 던지기
                    player.ExecuteCommand(PlayerController.Command.ThrowBranch);
                    break;
                case "O(상호작용)": // 상호작용 (근처 나뭇가지 회수 등)
                    player.ExecuteCommand(PlayerController.Command.Interact);
                    break;
                case "9": // 나뭇가지 세우기 (발판/장애물)
                    player.ExecuteCommand(PlayerController.Command.PlantBranch);
                    break;
                case "S": // 나뭇가지 돌아오기 (부메랑 회수)
                    player.ExecuteCommand(PlayerController.Command.ReturnBranch);
                    break;
                case "⋉": // 나뭇가지 위치로 텔레포트
                    player.ExecuteCommand(PlayerController.Command.TeleportBranch);
                    break;
                case "§": // 보스 스킬 (보스전에서 보스가 틈을 보일 때만 동작)
                    player.ExecuteCommand(PlayerController.Command.Boss);
                    break;
                default:
                    // 이름은 위에서 이미 로그됨. 연결된 동작만 아직 없음.
                    break;
            }
            return; // 템플릿으로 인식되면 방향 로직은 건너뜀
        }

        // 2) 기존 방향 시퀀스(좌우/점프/슬라이드)로 폴백
        List<Dir> seq = BuildDirectionSequence();

        // ㄱ 공격 (나뭇가지 든 상태에서만 실제 발동). 획 순서 상관없이 같은 ㄱ 모양이면 인식:
        //  · 오른쪽 공격 = ㄱ(오른쪽→아래) 또는 역순(위→왼쪽)
        //  · 왼쪽 공격  = ㄱ반대(왼쪽→아래) 또는 역순(위→오른쪽)
        //  모서리 기준 두 획 판정(TryCornerAttack)을 우선 — 세로획을 비스듬히 내리그은 손글씨 ㄱ도 인식.
        bool cornerRight;
        bool cornerAttack = TryCornerAttack(out cornerRight);
        bool attackForward = seq.Count == 2 && seq[1] == Dir.Down && (seq[0] == Dir.Right || seq[0] == Dir.Left);
        bool attackReverse = seq.Count == 2 && seq[0] == Dir.Up && (seq[1] == Dir.Left || seq[1] == Dir.Right);
        // S(← ↓ → ↓ ←)는 방향 시퀀스에서 '→' 조각이 버려져 ← ↓ (ㄱ반대)로 뭉개질 수 있다.
        // ㄱ은 좌우 방향이 많아야 한 번 바뀌지만 S는 두 번 바뀌므로, 두 번 이상 바뀐 획은 공격이 아님.
        bool zigzag = HorizontalReversals() >= 2;
        if (zigzag && (cornerAttack || attackForward || attackReverse))
        {
            cornerAttack = attackForward = attackReverse = false;
            // 템플릿 점수가 기준엔 못 미쳤어도 가장 가까운 모양이 S면 S(돌아오기)로 처리
            if (name == "S" && score >= sFallbackThreshold)
            {
                Debug.Log($"[제스처] 'S' 인식됨 (지그재그 보정, 점수 {score:F2})");
                FlashTrail(TemplateColor(name));
                PlayGesture(GestureClip(name));
                player.ExecuteCommand(PlayerController.Command.ReturnBranch);
                return;
            }
        }
        if (cornerAttack || attackForward || attackReverse)
        {
            bool right = cornerAttack ? cornerRight
                       : attackForward ? (seq[0] == Dir.Right) : (seq[1] == Dir.Left);
            Debug.Log("[제스처] 'ㄱ" + (right ? "" : "반대") + "'(" + (right ? "오른쪽" : "왼쪽") + " 공격) 인식됨"
                      + $" [판정: {(cornerAttack ? "모서리" : attackForward ? "정순" : "역순")}, 좌우전환 {HorizontalReversals()}, 템플릿 {name} {score:F2}]");
            Debug.Log("[제스처-획] " + StrokeDump()); // S 등이 ㄱ으로 오인될 때 원인 분석용
            FlashTrail(colorAttack); // ㄱ 공격 색
            PlayGesture(sfxAttack); // ㄱ 공격 인식음
            player.ExecuteCommand(right ? PlayerController.Command.AttackRight : PlayerController.Command.AttackLeft);
            return;
        }

        PlayerController.Command cmd = Match(seq);

        // 좌우 이동은 그은 가로 길이에 비례해 이동 거리를 정한다 (길게 그으면 멀리)
        if (cmd == PlayerController.Command.MoveRight || cmd == PlayerController.Command.MoveLeft)
        {
            int dir = cmd == PlayerController.Command.MoveRight ? 1 : -1;
            float px = HorizontalSpanPx();
            Debug.Log($"[제스처] '{(dir > 0 ? "ㅡ→" : "←ㅡ")}' 이동 (그은 길이 {px:F0}px)");
            PlayGesture(sfxMove); // 좌우 이동 인식음
            player.StartMoveByLength(dir, px);
            return;
        }

        if (cmd != PlayerController.Command.None)
        {
            Debug.Log($"[제스처] {CmdLabel(cmd)}");
            if (cmd == PlayerController.Command.SlideRight || cmd == PlayerController.Command.SlideLeft)
            {
                FlashTrail(colorSlide); // 슬라이드 색 (점프는 기본 이동이라 흰색 유지)
                PlayGesture(sfxSlide); // 슬라이드 인식음
            }
            else if (cmd == PlayerController.Command.Jump)
                PlayGesture(sfxJump); // 점프 인식음
            player.ExecuteCommand(cmd);
        }
        else if (StrokeStraightness() >= looseStraightness)
        {
            // 템플릿·꺾임 모두 아니지만 '대체로 곧은' 획(아치형·물결 모양으로 그은 이동 등) → 시작→끝 방향으로 판정
            HandleStraightStroke();
        }
        else
        {
            Debug.Log($"[제스처] 인식 안 됨 (그린 방향: {SeqText(seq)})");
            FailFeedback();
        }
    }

    /// <summary>곧은 획 처리 — 시작→끝 벡터 하나로 판정(중간 떨림·끝 삐침 무시).
    /// 위쪽 ±45° 안 = 점프, 아래쪽 ±45° 안 = 인식 안 됨, 나머지 = 좌/우 이동.</summary>
    void HandleStraightStroke()
    {
        Vector2 v = points[points.Count - 1] - points[0];
        if (v.magnitude < minStraightLength) return; // 짧은 클릭/실수로 끈 것 — 조용히 무시

        if (v.y > 0f && Mathf.Abs(v.x) <= v.y)
        {
            Debug.Log($"[제스처] {CmdLabel(PlayerController.Command.Jump)}");
            PlayGesture(sfxJump); // 점프 인식음
            player.ExecuteCommand(PlayerController.Command.Jump);
            return;
        }
        if (-v.y > Mathf.Abs(v.x))
        {
            Debug.Log("[제스처] 인식 안 됨 (그린 방향: ↓)");
            FailFeedback();
            return;
        }

        int dir = v.x > 0f ? 1 : -1;
        float px = HorizontalSpanPx();
        Debug.Log($"[제스처] '{(dir > 0 ? "ㅡ→" : "←ㅡ")}' 이동 (그은 길이 {px:F0}px)");
        PlayGesture(sfxMove); // 좌우 이동 인식음
        player.StartMoveByLength(dir, px);
    }

    /// <summary>인식 실패 피드백(실패음 + 화면 흔들림). 실제로 뭔가 '그린' 시도일 때만 (짧은 클릭/점은 무시).</summary>
    void FailFeedback()
    {
        if (StrokeReach() <= minStraightLength) return;
        if (failClip != null && sfx != null) sfx.PlayOneShot(failClip, failVolume); // 실패음
        ScreenFeedback.Ensure().FailFeedback(); // 화면 살짝 흔들림 + 빨간 플래시
    }

    /// <summary>시작점에서 가장 멀리 간 거리(px). 짧은 클릭과 실제 획을 구분하는 데 쓴다.</summary>
    float StrokeReach()
    {
        float r = 0f;
        for (int i = 0; i < points.Count; i++) r = Mathf.Max(r, points[i].magnitude);
        return r;
    }

    /// <summary>방향 시퀀스로 인식된 명령의 한글 라벨 (콘솔 표시용).</summary>
    static string CmdLabel(PlayerController.Command cmd)
    {
        switch (cmd)
        {
            case PlayerController.Command.Jump: return "'ㅣ' 점프";
            case PlayerController.Command.SlideRight: return "'ㄴ' 오른쪽 슬라이드";
            case PlayerController.Command.SlideLeft: return "'ㄴ반대' 왼쪽 슬라이드";
            default: return cmd.ToString();
        }
    }

    /// <summary>인식 실패 시 그린 방향 순서를 화살표로 표시.</summary>
    string SeqText(List<Dir> seq)
    {
        if (seq.Count == 0) return "없음";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < seq.Count; i++)
        {
            if (i > 0) sb.Append("→");
            sb.Append(DirArrow(seq[i]));
        }
        return sb.ToString();
    }

    static string DirArrow(Dir d)
    {
        switch (d)
        {
            case Dir.Up: return "↑";
            case Dir.Down: return "↓";
            case Dir.Left: return "←";
            case Dir.Right: return "→";
            default: return "·";
        }
    }

    /// <summary>획의 '곧은 정도' = 끝점 직선거리 / 실제 경로 길이 (0~1, 1에 가까울수록 직선).
    /// 직선이면 곡선형 템플릿(N·O·S·9·⋉·§)이 아니라 이동/점프이므로 템플릿 인식을 건너뛰는 판단에 쓴다.</summary>
    float StrokeStraightness()
    {
        if (points.Count < 2) return 1f;
        float path = 0f;
        for (int i = 1; i < points.Count; i++) path += Vector2.Distance(points[i], points[i - 1]);
        if (path < 1e-4f) return 1f;
        return Vector2.Distance(points[points.Count - 1], points[0]) / path;
    }

    /// <summary>그은 궤적의 가로 폭(px). 좌우 이동 거리 계산에 쓴다.</summary>
    float HorizontalSpanPx()
    {
        if (points.Count == 0) return 0f;
        float min = points[0].x, max = points[0].x;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i].x < min) min = points[i].x;
            if (points[i].x > max) max = points[i].x;
        }
        return max - min;
    }

    // ── 템플릿 녹화/저장 ──────────────────────────────

    // ── 전체 녹화 (F9) ──────────────────────────────

    void StartFullRecording()
    {
        if (recordSequence == null || recordSequence.Length == 0 || recordsPerShape <= 0) return;
        fullRecording = true;
        recIndex = 0;
        recNew.Clear();
        Debug.Log($"[녹화] 전체 녹화 시작 — 모양 {recordSequence.Length}개 × {recordsPerShape}번. 첫 모양: '{recordSequence[0]}'");
    }

    int RecordedCount(string name) { return recNew.FindAll(x => x.name == name).Count; }

    void RecordForSequence()
    {
        string name = recordSequence[recIndex];
        if (points.Count < 8)
        {
            Debug.LogWarning("[녹화] 획이 너무 짧아 저장 안 함 — 좀 더 크게 그려주세요.");
            if (failClip != null && sfx != null) sfx.PlayOneShot(failClip, failVolume);
            return;
        }
        recNew.Add(new UnistrokeRecognizer.Template { name = name, points = UnistrokeRecognizer.Normalize(points) });
        FlashTrail(TemplateColor(name));
        int n = RecordedCount(name);
        Debug.Log($"[녹화] '{name}' {n}/{recordsPerShape}");
        if (n >= recordsPerShape)
        {
            recIndex++;
            if (recIndex >= recordSequence.Length) FinishFullRecording();
            else Debug.Log($"[녹화] 다음 모양: '{recordSequence[recIndex]}'");
        }
    }

    /// <summary>직전에 녹화한 획 하나를 지운다 (잘못 그렸을 때).</summary>
    void UndoFullRecording()
    {
        if (recNew.Count == 0) return;
        var last = recNew[recNew.Count - 1];
        recNew.RemoveAt(recNew.Count - 1);
        // 앞 모양으로 넘어간 직후라면 그 모양으로 돌아감
        int idx = System.Array.IndexOf(recordSequence, last.name);
        if (idx >= 0) recIndex = idx;
        Debug.Log($"[녹화] 직전 '{last.name}' 지움 — 지금 '{recordSequence[recIndex]}' {RecordedCount(recordSequence[recIndex])}/{recordsPerShape}");
    }

    /// <summary>이번 녹화를 공용 파일(하나, 모든 사람 것이 쌓임)에 추가하고, 이 PC 사본에도 추가한다.
    /// 공용 파일은 에디터에서만 쓸 수 있다(빌드에선 사본에만 저장 → 불러올 때 합쳐짐).</summary>
    void FinishFullRecording()
    {
        fullRecording = false;

        // 1) 이 PC 사본에 추가 (패키지로 공용 파일이 교체돼도 여기서 되살림)
        var mine = ReadStore(MyCopyPath);
        mine.AddRange(recNew);
        WriteStore(MyCopyPath, mine);

#if UNITY_EDITOR
        // 2) 공용 파일에 추가 (기존 것은 그대로 두고 뒤에 붙임)
        var shared = ReadStore(SharedFilePath);
        shared.AddRange(recNew);
        WriteStore(SharedFilePath, shared);
        UnityEditor.AssetDatabase.ImportAsset(SharedFilePath);
        Debug.Log($"[녹화] 전체 녹화 완료 — {recNew.Count}개 추가 (공용 파일 총 {shared.Count}개, 이 PC 사본 {mine.Count}개)");
#else
        Debug.Log($"[녹화] 전체 녹화 완료 — {recNew.Count}개를 이 PC 사본에 저장 (총 {mine.Count}개)");
#endif
        LoadTemplates();
    }

    static void WriteStore(string path, List<UnistrokeRecognizer.Template> list)
    {
        var store = new SerStore { version = UnistrokeRecognizer.FormatVersion };
        foreach (var t in list) store.items.Add(ToSer(t));
        try
        {
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(store, true));
        }
        catch (System.Exception e) { Debug.LogWarning($"[녹화] 저장 실패({path}): {e.Message}"); }
    }

    [System.Serializable] class SerTemplate { public string name; public List<Vector2> points; public bool legacy; }
    [System.Serializable] class SerStore { public int version; public List<SerTemplate> items = new List<SerTemplate>(); }

    /// <summary>옛날 형식 파일에서도 그대로 가져다 쓸 모양 (예전 방식=회전 무관으로 비교). §는 예전 녹화 유지.</summary>
    static readonly string[] legacyKeepNames = { "§" };

    /// <summary>JSON 문자열을 템플릿 리스트로 변환 (실패하면 빈 리스트).
    /// 형식 버전이 옛날 것(회전시켜 저장한 템플릿)이면 legacyKeepNames 모양만 legacy 로 가져오고 나머지는 버린다.</summary>
    List<UnistrokeRecognizer.Template> ParseStore(string json)
    {
        var list = new List<UnistrokeRecognizer.Template>();
        if (string.IsNullOrEmpty(json)) return list;
        var store = JsonUtility.FromJson<SerStore>(json);
        if (store?.items == null) return list;
        bool old = store.version < UnistrokeRecognizer.FormatVersion;
        foreach (var it in store.items)
        {
            if (old && System.Array.IndexOf(legacyKeepNames, it.name) < 0) continue;
            list.Add(new UnistrokeRecognizer.Template { name = it.name, points = it.points, legacy = old || it.legacy });
        }
        if (old) Debug.LogWarning($"[녹화] 옛날 형식 템플릿 — {string.Join(",", legacyKeepNames)} 만 예전 방식으로 쓰고 나머지는 무시. F9 로 다시 녹화하세요.");
        return list;
    }

    static SerTemplate ToSer(UnistrokeRecognizer.Template t)
    {
        return new SerTemplate { name = t.name, points = t.points, legacy = t.legacy };
    }

    /// <summary>JSON 파일을 읽어 템플릿 리스트로 변환 (없거나 실패하면 빈 리스트).</summary>
    List<UnistrokeRecognizer.Template> ReadStore(string path)
    {
        try
        {
            if (System.IO.File.Exists(path)) return ParseStore(System.IO.File.ReadAllText(path));
        }
        catch (System.Exception e) { Debug.LogWarning($"[녹화] 템플릿 읽기 실패({path}): {e.Message}"); }
        return new List<UnistrokeRecognizer.Template>();
    }

    void LoadTemplates()
    {
        templates.Clear();

        bundledVersion = -1;
        sharedCount = myCopyCount = restoredCount = 0;

        // 1) 공용 녹화 파일 (모든 사람 것이 하나에) — 에디터에선 파일을 직접 읽어 방금 저장한 것도 바로 반영
        string sharedJson = null;
#if UNITY_EDITOR
        if (System.IO.File.Exists(SharedFilePath)) sharedJson = System.IO.File.ReadAllText(SharedFilePath);
#endif
        if (sharedJson == null)
        {
            var ta = Resources.Load<TextAsset>(SharedResourceName);
            if (ta != null) sharedJson = ta.text;
        }
        if (sharedJson != null)
        {
            try
            {
                var s = JsonUtility.FromJson<SerStore>(sharedJson);
                bundledVersion = s != null ? s.version : -1;
                templates.AddRange(ParseStore(sharedJson));
            }
            catch (System.Exception e) { Debug.LogWarning($"[녹화] 공용 녹화 파일 읽기 실패: {e.Message}"); }
        }
        sharedCount = templates.Count;

        // 2) 이 PC 사본 — 공용 파일에 없는 것(패키지로 교체돼 빠진 내 녹화)은 되살려 넣는다
        var restored = new List<UnistrokeRecognizer.Template>();
        var mine = ReadStore(MyCopyPath);
        myCopyCount = mine.Count;
        foreach (var t in mine)
        {
            bool have = false;
            for (int i = 0; i < templates.Count; i++)
                if (SameTemplate(templates[i], t)) { have = true; break; }
            if (!have) { templates.Add(t); restored.Add(t); }
        }
        restoredCount = restored.Count;
#if UNITY_EDITOR
        if (restored.Count > 0 && sharedJson != null)
        {
            // 공용 파일에도 다시 써 넣어서, 다음에 패키지로 보낼 때 같이 가게
            var shared = ReadStore(SharedFilePath);
            shared.AddRange(restored);
            WriteStore(SharedFilePath, shared);
            UnityEditor.AssetDatabase.ImportAsset(SharedFilePath);
            Debug.Log($"[녹화] 공용 파일에 빠져 있던 이 PC 녹화 {restored.Count}개를 다시 넣었습니다.");
        }
#endif

        // 공용 파일이 없으면 예전 방식(번들 에셋 하나)으로
        if (sharedJson == null)
        {
            if (bundledTemplates != null)
            {
                try
                {
                    var s = JsonUtility.FromJson<SerStore>(bundledTemplates.text);
                    bundledVersion = s != null ? s.version : -1;
                    foreach (var t in ParseStore(bundledTemplates.text)) templates.Add(t);
                }
                catch (System.Exception e) { Debug.LogWarning($"[녹화] 번들 템플릿 파싱 실패: {e.Message}"); }
            }
            else
            {
                foreach (var t in ReadStore(BundledTemplatePath)) templates.Add(t);
            }
        }
        Debug.Log($"[녹화] 템플릿 {templates.Count}개");
    }

    /// <summary>두 템플릿이 사실상 동일한가 (이름 + 궤적 점들이 같음). 공용 파일·사본 중복 확인용.</summary>
    static bool SameTemplate(UnistrokeRecognizer.Template a, UnistrokeRecognizer.Template b)
    {
        if (a.name != b.name) return false;
        if (a.points == null || b.points == null) return a.points == b.points;
        if (a.points.Count != b.points.Count) return false;
        for (int i = 0; i < a.points.Count; i++)
            if ((a.points[i] - b.points[i]).sqrMagnitude > 1e-8f) return false;
        return true;
    }

    // ── 방향 시퀀스(기존 로직) ──────────────────────────────

    /// <summary>궤적 → 방향 조각 시퀀스.</summary>
    List<Dir> BuildDirectionSequence()
    {
        // 1) 각 구간 방향 + 길이를 같은 방향끼리 병합
        var runs = new List<KeyValuePair<Dir, float>>();
        float pathLen = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 d = points[i] - points[i - 1];
            if (d.sqrMagnitude < 0.0001f) continue;
            pathLen += d.magnitude;
            Dir dir = Dominant(d);
            if (runs.Count > 0 && runs[runs.Count - 1].Key == dir)
                runs[runs.Count - 1] = new KeyValuePair<Dir, float>(dir, runs[runs.Count - 1].Value + d.magnitude);
            else
                runs.Add(new KeyValuePair<Dir, float>(dir, d.magnitude));
        }
        // 2) 짧은 조각 제거 — 절대 최소값 또는 전체 길이 대비 비율 중 큰 쪽 미만이면 손떨림으로 본다
        //    (작게 그린 ㄴ·ㄱ도 인식되고, 크게 그린 획의 모서리 뭉개짐도 무시)
        float cut = Mathf.Max(minSegmentLength, pathLen * minSegmentRatio);
        runs.RemoveAll(r => r.Value < cut);
        // 남은 조각이 획의 대부분을 차지하지 못하면(많이 버려짐 = 망가진 모양) 빈 시퀀스 → 인식 실패
        float kept = 0f;
        foreach (var r in runs) kept += r.Value;
        if (pathLen > 0f && kept / pathLen < minSequenceCoverage) return new List<Dir>();
        // 3) 제거 후 인접 같은 방향 재병합 (길이도 합산)
        var merged = new List<KeyValuePair<Dir, float>>();
        foreach (var r in runs)
        {
            if (merged.Count > 0 && merged[merged.Count - 1].Key == r.Key)
                merged[merged.Count - 1] = new KeyValuePair<Dir, float>(r.Key, merged[merged.Count - 1].Value + r.Value);
            else
                merged.Add(r);
        }
        // 4) 꺾인 획이면, 시작→끝 실제 이동에 반영되지 않은 조각은 버린다
        //    (예: 아치형으로 그은 이동의 내리막은 결국 제자리 높이로 돌아오므로 ↓ 조각이 아님 → '→'만 남아 이동)
        //    진짜 ㄴ·ㄱ은 두 조각 모두 끝점 위치에 그대로 반영돼 유지된다.
        if (merged.Count >= 2 && points.Count >= 2)
        {
            Vector2 net = points[points.Count - 1] - points[0];
            merged.RemoveAll(r => Vector2.Dot(net, DirVector(r.Key)) < r.Value * 0.5f);
        }
        var result = new List<Dir>();
        foreach (var r in merged)
            if (result.Count == 0 || result[result.Count - 1] != r.Key)
                result.Add(r.Key);
        return result;
    }

    /// <summary>ㄱ 공격 전용 판정 — 획에서 가장 많이 꺾인 점(시작–끝 직선에서 가장 먼 점)을 모서리로 보고
    /// 두 획으로 나눠, 각 획의 전체 방향으로 판정한다. 점 단위 4방향 판정과 달리 비스듬한 획에 강하다.
    ///  · 정순: 1획 가로(±attackHorizontalTol°) → 2획 아래. 1획이 오른쪽이면 오른쪽 공격.
    ///  · 역순: 1획 위 → 2획 가로(±attackHorizontalTol°). 2획이 왼쪽이면 오른쪽 공격(ㄱ을 거꾸로 그린 것).
    ///  세로획 기울기는 안쪽(attackLeanIn, 손글씨 ㄱ)은 넉넉히, 바깥쪽(attackLeanOut)은 좁게 허용 — 아치형 이동 오인식 방지.</summary>
    bool TryCornerAttack(out bool right)
    {
        right = false;
        if (points.Count < 3) return false;
        Vector2 a = points[0], b = points[points.Count - 1];
        Vector2 chord = b - a;
        if (chord.sqrMagnitude < 1f) return false;

        // 모서리 = 시작–끝 직선에서 수직 거리가 가장 먼 점
        Vector2 n = new Vector2(-chord.y, chord.x).normalized;
        int ci = -1; float best = 0f;
        for (int i = 1; i < points.Count - 1; i++)
        {
            float d = Mathf.Abs(Vector2.Dot(points[i] - a, n));
            if (d > best) { best = d; ci = i; }
        }
        if (ci < 0) return false;

        Vector2 leg1 = points[ci] - a, leg2 = b - points[ci];
        float len1 = leg1.magnitude, len2 = leg2.magnitude;
        if (len1 < minSegmentLength || len2 < minSegmentLength) return false;
        // 두 획 길이가 너무 차이 나면(한쪽이 삐침 수준) ㄱ이 아님
        if (Mathf.Min(len1, len2) < Mathf.Max(len1, len2) * attackMinLegRatio) return false;
        // 각 획이 대체로 곧아야 함(구불구불한 획/다른 모양 배제)
        if (SubPathLength(0, ci) > len1 / attackLegStraightness || SubPathLength(ci, points.Count - 1) > len2 / attackLegStraightness) return false;

        float ang1 = Mathf.Atan2(leg1.y, leg1.x) * Mathf.Rad2Deg; // 0=오른쪽, 90=위, -90=아래, ±180=왼쪽
        float ang2 = Mathf.Atan2(leg2.y, leg2.x) * Mathf.Rad2Deg;

        bool h1Right = Mathf.Abs(Mathf.DeltaAngle(ang1, 0f)) <= attackHorizontalTol;
        bool h1Left  = Mathf.Abs(Mathf.DeltaAngle(ang1, 180f)) <= attackHorizontalTol;
        bool h2Right = Mathf.Abs(Mathf.DeltaAngle(ang2, 0f)) <= attackHorizontalTol;
        bool h2Left  = Mathf.Abs(Mathf.DeltaAngle(ang2, 180f)) <= attackHorizontalTol;

        // 정순 ㄱ(→ 후 ↓): 세로획이 안쪽(왼쪽)으로 기울면 각도가 -90보다 작아짐(-90-lean)
        if (h1Right && LeanOk(-Mathf.DeltaAngle(-90f, ang2))) { right = true; return true; }
        // 정순 ㄱ반대(← 후 ↓): 거울상 — 안쪽(오른쪽)으로 기울면 -90+lean
        if (h1Left && LeanOk(Mathf.DeltaAngle(-90f, ang2))) { right = false; return true; }
        // 역순 ㄱ(↑ 후 ←): 세로획을 거꾸로 그은 것 — 안쪽 기울기면 90-lean
        if (h2Left && LeanOk(-Mathf.DeltaAngle(90f, ang1))) { right = true; return true; }
        // 역순 ㄱ반대(↑ 후 →): 거울상 — 90+lean
        if (h2Right && LeanOk(Mathf.DeltaAngle(90f, ang1))) { right = false; return true; }
        return false;
    }

    /// <summary>획 좌표를 짧은 문자열로 (디버그 로그용, 최대 40점 정도로 솎음)</summary>
    string StrokeDump()
    {
        var sb = new System.Text.StringBuilder();
        int step = Mathf.Max(1, points.Count / 40);
        for (int i = 0; i < points.Count; i += step)
            sb.Append(Mathf.RoundToInt(points[i].x)).Append(',').Append(Mathf.RoundToInt(points[i].y)).Append(' ');
        var last = points[points.Count - 1];
        sb.Append(Mathf.RoundToInt(last.x)).Append(',').Append(Mathf.RoundToInt(last.y));
        return sb.ToString();
    }

    /// <summary>획이 좌우 방향을 바꾼 횟수. 손떨림은 무시하도록 reversalTolerance(px) 이상 되돌아갔을 때만 센다.</summary>
    int HorizontalReversals()
    {
        if (points.Count < 3) return 0;
        int dir = 0, count = 0;
        float extreme = points[0].x; // 현재 방향으로 간 가장 먼 x
        for (int i = 1; i < points.Count; i++)
        {
            float x = points[i].x;
            if (dir == 0)
            {
                if (Mathf.Abs(x - extreme) >= reversalTolerance) { dir = x > extreme ? 1 : -1; extreme = x; }
            }
            else if ((x - extreme) * dir > 0f) extreme = x; // 같은 방향으로 더 감
            else if (Mathf.Abs(x - extreme) >= reversalTolerance) { dir = -dir; extreme = x; count++; }
        }
        return count;
    }

    /// <summary>세로획 기울기(lean, 도) 허용 여부. +는 안쪽(손글씨 ㄱ 방향), -는 바깥쪽.</summary>
    bool LeanOk(float lean) { return lean <= attackLeanIn && lean >= -attackLeanOut; }

    /// <summary>points[from]~points[to] 구간의 실제 경로 길이.</summary>
    float SubPathLength(int from, int to)
    {
        float s = 0f;
        for (int i = from + 1; i <= to; i++) s += Vector2.Distance(points[i], points[i - 1]);
        return s;
    }

    static Vector2 DirVector(Dir d)
    {
        switch (d)
        {
            case Dir.Up: return Vector2.up;
            case Dir.Down: return Vector2.down;
            case Dir.Left: return Vector2.left;
            case Dir.Right: return Vector2.right;
            default: return Vector2.zero;
        }
    }

    /// <summary>더 큰 축이 이기는 4방향 판정.</summary>
    static Dir Dominant(Vector2 d)
    {
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            return d.x > 0 ? Dir.Right : Dir.Left;
        return d.y > 0 ? Dir.Up : Dir.Down;
    }

    /// <summary>방향 시퀀스 → 명령표 매칭.</summary>
    PlayerController.Command Match(List<Dir> seq)
    {
        if (seq.Count == 1)
        {
            if (seq[0] == Dir.Right) return PlayerController.Command.MoveRight;
            if (seq[0] == Dir.Left)  return PlayerController.Command.MoveLeft;
            if (seq[0] == Dir.Up)    return PlayerController.Command.Jump;
        }
        else if (seq.Count == 2 && seq[0] == Dir.Down)
        {
            // ㄴ(아래→오른쪽)=오른쪽 슬라이드, 반대 ㄴ(아래→왼쪽)=왼쪽 슬라이드
            if (seq[1] == Dir.Right) return PlayerController.Command.SlideRight;
            if (seq[1] == Dir.Left)  return PlayerController.Command.SlideLeft;
        }
        return PlayerController.Command.None;
    }

    /// <summary>F10: 지금 불러온 템플릿 상태를 보여줌 — 패키지로 받은 PC에서 녹화본이 제대로 들어왔는지 확인용.</summary>
    void DrawCheckPanel()
    {
        var sb = new System.Text.StringBuilder();
        bool okAll = true;

        bool verOk = bundledVersion >= UnistrokeRecognizer.FormatVersion;
        if (!verOk) okAll = false;
        sb.AppendLine($"형식 버전 {(bundledVersion < 0 ? "없음" : bundledVersion.ToString())} {(verOk ? "OK" : "✗ 옛날 형식/없음 — 새 파일 필요")}   인식 기준 {recognizeThreshold:F2}");
        sb.AppendLine($"공용 녹화 파일: {sharedCount}개   이 PC 사본: {myCopyCount}개{(restoredCount > 0 ? $"   (공용에 없어서 되살림 {restoredCount}개)" : "")}");
        sb.AppendLine("");

        // 모양별 합계 (예전 방식 표시)
        sb.AppendLine("모양별 합계:");
        var names = new List<string>(recordSequence);
        foreach (var t in templates) if (!names.Contains(t.name)) names.Add(t.name);
        foreach (var n in names)
        {
            int c = 0; bool legacy = false;
            foreach (var t in templates)
            {
                if (t.name != n) continue;
                c++;
                if (t.legacy) legacy = true;
            }
            bool missing = c == 0;
            if (missing) okAll = false;
            sb.AppendLine($"  {n} : {c}개{(legacy ? "  (예전 방식)" : "")}{(missing ? "  ✗ 없음 — 인식 안 됨" : "")}");
        }
        sb.AppendLine("");
        sb.AppendLine(okAll ? "✓ 모든 모양 준비됨" : "✗ 문제 있음 — 위 ✗ 확인");
        sb.Append("F10: 닫기");

        var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = false };
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(16, 16, 12, 12);
        var content = new GUIContent(sb.ToString());
        Vector2 size = style.CalcSize(content);
        var r = new Rect(110, 90, Mathf.Max(420f, size.x), size.y);
        GUI.color = new Color(1f, 1f, 1f, 1f);
        GUI.Box(r, GUIContent.none);
        GUI.Box(r, GUIContent.none); // 두 번 그려 배경을 진하게
        GUI.Label(r, content, style);
    }

    /// <summary>녹화 중임을 화면에 표시(개발용).</summary>
    void OnGUI()
    {
        if (showCheck) DrawCheckPanel();
        if (!fullRecording) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.red;
        if (fullRecording)
        {
            string name = recordSequence[recIndex];
            GUI.Label(new Rect(110, 14, 1200, 34),
                $"● 전체 녹화 ({recIndex + 1}/{recordSequence.Length}) — '{name}' 그리기 {RecordedCount(name) + 1}/{recordsPerShape}번째", style);
            style.fontSize = 16; style.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(110, 48, 1200, 26), "우클릭: 이번 획 취소   Backspace: 직전 것 지우기   F9: 지금까지 그린 것만 저장하고 끝", style);
        }
    }
}
