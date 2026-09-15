# The Photo Project — เอกสารระบบละเอียด

> เวอร์ชันย่ออ่านเร็วอยู่ที่ [README.md](README.md) — ไฟล์นี้คือรายละเอียดทางเทคนิคของทุกสคริปต์ใน `Assets/[02]Code/Script/`
>
> **อัปเดตล่าสุด:** 2026-09-15 (Unity 6000.3.19f1, Input System ใหม่, Yarn Spinner, Cinemachine 3)

---

## สารบัญ

1. [State Manager (แกนกลาง)](#1-state-manager-แกนกลาง)
2. [Player — การเคลื่อนที่ผู้เล่น](#2-player--การเคลื่อนที่ผู้เล่น)
3. [Camera — กล้องและระบบถ่ายรูป](#3-camera--กล้องและระบบถ่ายรูป)
4. [Creature — สัตว์/พืช และ AI](#4-creature--สัตว์พืช-และ-ai)
5. [Journal — สมุดบันทึก](#5-journal--สมุดบันทึก)
6. [Quest — เควสและ NPC](#6-quest--เควสและ-npc)
7. [Storage — คลังภาพ](#7-storage--คลังภาพ)
8. [Audio](#8-audio)
9. [Environment — สร้างสิ่งแวดล้อมจาก Terrain](#9-environment--สร้างสิ่งแวดล้อมจาก-terrain)
10. [UI — HUD กลางจอ](#10-ui--hud-กลางจอ)
11. [Main Menu](#11-main-menu)
12. [ข้อจำกัด/สิ่งที่ยังไม่มีตอนนี้](#12-ข้อจำกัดสิ่งที่ยังไม่มีตอนนี้)
13. [กติกาการอัปเดตเอกสารนี้](#13-กติกาการอัปเดตเอกสารนี้)

---

## 1. State Manager (แกนกลาง)

### `StateManager.cs` — `Assets/[02]Code/Script/Player/StateManager.cs`

Singleton (`StateManager.Instance`) คุมสถานะกลางของเกม 2 กลุ่ม ไม่ persist ข้าม Scene (ไม่ใช้ `DontDestroyOnLoad`):

- **`MovementState`**: `Idle, Walking, Running, Crouch` — ใช้ขับ Animator เป็นหลัก (หมายเหตุ: `Running` มีประกาศไว้ใน enum แต่ยังไม่มีใครสั่งใช้จริง ดูหัวข้อ 10)
- **`SystemState`**: `Normal, Photograph, Talking, Pause, Journal, Storage` — คุมว่าระบบ Input/UI ไหนควรทำงานได้ตอนนี้

Event หลัก: `OnMovementStateChanged`, `OnSystemStateChanged` (ส่ง old/new state) — ระบบอื่นทั้งหมด Subscribe แทนที่จะ Reference กันตรงๆ

Helper method สำคัญที่ระบบอื่นเรียกเช็คก่อนรับ Input:
- `CanControlPlayer()` — คืน `true` เฉพาะตอน `Normal` เท่านั้น
- `CanCrouch()` — คืน `true` ตอน `Normal` และ `Photograph` (ย่อได้แม้กำลังเล็งกล้องถ่ายรูปอยู่)

ทุกระบบ UI (Journal/Storage/Photo/NPC) เรียก `SetSystemState()` ตอนเปิด แล้วเซ็ตกลับ `Normal` ตอนปิด เพื่อบล็อกไม่ให้ระบบอื่นทำงานซ้อนกัน

---

## 2. Player — การเคลื่อนที่ผู้เล่น

โฟลเดอร์ `Assets/[02]Code/Script/Player/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `PlayerMovement.cs` | `PlayerMovement` | เดิน/กระโดดผ่าน `Rigidbody`, เช็คพื้น (Raycast), รองรับทางลาด (Slope), จำกัดความเร็ว, รายงาน `MovementState` (Idle/Walking/Crouch) ให้ `StateManager` ทุกเฟรม |
| `PlayerSprint.cs` | `PlayerSprint` (ต้องมี `PlayerMovement` บน Object เดียวกัน) | เพิ่มระบบวิ่งแบบไม่แก้ `PlayerMovement` เดิม — override `moveSpeed` ชั่วคราวแบบ Lerp นุ่มนวล มีระบบ Stamina เป็นออปชัน |
| `Playercrouch.cs` | `PlayerCrouch` | Toggle ย่อ/ลุก ปรับความสูงกล้อง **ทุกตัวที่ผูกไว้ใน List** พร้อมกันแบบนุ่มนวล (ใช้ค่า Offset ไม่ใช่ความสูงตายตัว เพราะกล้องแต่ละตัวเริ่มต้นคนละระดับ) |
| `StateManager.cs` | `StateManager` | ดูหัวข้อ 1 |
| `FootIK.cs` | `FootIK` | Foot IK เต็มรูปแบบ: Raycast หาพื้นใต้เท้าซ้าย/ขวา, ปรับตำแหน่ง+มุมเท้าให้แนบพื้น, ปรับสะโพก (Hip) ตาม, ลด/ปิด IK อัตโนมัติเมื่อวิ่งเร็ว |
| `PlayerAnimationController.cs` | `PlayerAnimationController` | Subscribe `PlayerMovement.OnSpeedChanged/OnJumped` และ `StateManager.OnMovementStateChanged` แล้วส่งเข้า Animator Parameter (`Speed`, `Jump`, `IsWalking`, `IsRunning`, `IsCrouch`) |
| `InputManager.cs` | `InputManager` | ⚠️ **โค้ดที่ไม่มีใครเรียกใช้ (dead code)** — สร้าง `PlayerControls` แล้วเก็บ `movementInput` ของตัวเอง แต่ไม่มีสคริปต์ไหนอ้างอิงถึงคลาสนี้เลย ทุกสคริปต์ในเกมผูก `InputActionReference` ตรงๆ ของตัวเองแทน (ดูตัวอย่างในตารางนี้ทุกไฟล์) ปลอดภัยที่จะลบทิ้ง |
| `DebugGameReset.cs` | `DebugGameReset` | **เครื่องมือ Debug สำหรับ Playtest** — กดปุ่ม R แล้ว: ล้างเควสทั้งหมด (`QuestManager.ResetAllQuests()`) → ล้าง Photo Storage ทั้งหมด (`PhotoStorage.ClearAllPhotos()`) → โหลด Scene `mainMenuSceneName` (ค่าเริ่มต้น `"MainMenu"`) ใช้แทนการปิด-เปิดเกมใหม่ระหว่างเปลี่ยนคนเทส **ต้องถอดออกก่อนปล่อยเกมจริง** |

**Input**: ทุกสคริปต์ในตารางนี้ (ยกเว้น `InputManager`) ผูก `public InputActionReference` ของตัวเองใน Inspector แล้ว `Enable()`/subscribe `.performed` เอง ไม่มีจุดกลางแบบ Input Manager

---

## 3. Camera — กล้องและระบบถ่ายรูป

โฟลเดอร์ `Assets/[02]Code/Script/Camera/` (ใช้ Cinemachine 3)

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `ThirdPersonCam.cs` | `ThirdPersonCam` | หมุนโมเดลผู้เล่น (`playerObj`) ให้หันตามทิศ WASD เทียบกับทิศกล้อง เช็ค `StateManager.CanControlPlayer()` ก่อนอ่าน Input เหมือน `PlayerMovement` ตัวละครจึงหยุดหันตาม WASD ตอนอยู่ในโหมดถ่ายรูป/คุย NPC/เปิดเมนูเช่นกัน |
| `CameraShoulderSwitch.cs` | `CameraShoulderSwitch` (ต้องมี `CinemachineRotationComposer`) | สลับกล้องไหล่ซ้าย/ขวา (Over-the-shoulder) ด้วยปุ่มเดียว |
| `Cameracontroller.cs` | `CameraController` | สลับ Priority ระหว่าง Vcam เดิน (Third Person) กับ Vcam ถ่ายรูป (Photo Cam) ตาม `StateManager.SystemState` — ไม่มี Logic เดิน/ถ่ายรูปเอง แค่ "ฟัง" state แล้วสลับกล้อง รีเซ็ต Pan/Tilt ของกล้องถ่ายรูปเป็น 0 ทุกครั้งก่อนเข้าโหมดถ่ายรูป |
| `PhotoTransitionUI.cs` | `PhotoTransitionUI` | คุม Overlay จอดำ 2 แบบ: `PlayTransition()` (เฟดดำคู่ขนานตอนตัดกล้อง ไม่รอเฟดเสร็จก่อนตัด) และ `PlayShutterFlash()` (แฟลชสั้นๆ ตอนกดชัตเตอร์) |

### `Camera/Photograph/` — ระบบถ่ายรูปโดยเฉพาะ

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `PhotoShooter.cs` | `PhotoShooter` | หัวใจของระบบถ่ายรูป — จัดการเข้า/ออกโหมดถ่ายรูป, ยิง `SphereCastAll` ตรวจจับวัตถุ Tag `"Photographable"` ที่มี `PhotoSubject`, อัปเดตสี Crosshair Real-time, Capture ภาพผ่าน `RenderTexture` แล้วยิง Event `OnPhotoCaptured(Texture2D, List<GameObject>)`. ระยะตรวจจับ (`EffectiveCastDistance`) ยืดอัตโนมัติตามระดับซูมจาก `PhotoZoom`. **หมายเหตุ:** การตรวจจับเป็นแค่ Cone/Sphere ด้านหน้ากล้อง ไม่เช็คว่าเฟรมสวย/อยู่กลางจอไหม |
| `PhotoZoom.cs` | `PhotoZoom` | ปรับ FOV กล้องถ่ายรูปทั้งฝั่งแสดงผล (`photoVcam`) และฝั่ง Capture (`captureCamera`) ให้ตรงกันเสมอ มี Motion Blur ตอนกำลังซูม (ผ่าน Volume) และเสียงคลิกตอนชนขอบซูม ทำงานเฉพาะ `SystemState.Photograph` |
| `PhotoSaveHandler.cs` | `PhotoSaveHandler` | Subscribe `PhotoShooter.OnPhotoCaptured` → เข้ารหัสเป็น PNG bytes → `Destroy()` Texture2D ทิ้งทันที (ประหยัด RAM) → ส่งต่อให้ `PhotoStorage.TryStorePhoto()` ดึง `creatureIds` จาก `PhotoSubject.CreatureId` ของแต่ละวัตถุที่ถ่ายติด |
| `Photogridui.cs` | `PhotoGridUI` | เปิด/ปิด Overlay เส้นกริด (เช่น Rule of Thirds) ตาม `SystemState.Photograph` เท่านั้น |

---

## 4. Creature — สัตว์/พืช และ AI

โฟลเดอร์ `Assets/[02]Code/Script/Creature/`

### ระบบ Identity/Profile (ใช้ร่วมกันทั้งสัตว์และพืช)

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Ijournalsubject.cs` | `IJournalSubject` (interface) | สัญญาว่าอะไรก็ตามที่จะโผล่ใน Journal ได้ ต้องมี `CreatureId` + `JournalEntry` |
| `Journalsubjectprofile.cs` | `JournalSubjectProfile` (abstract, `: ScriptableObject, IJournalSubject`) | ฐานร่วม เก็บแค่ `creatureId` + `journalEntry` — Unity Inspector ลาก Interface ตรงๆ ไม่ได้ เลยต้องมี abstract class นี้มาคั่น |
| `Creatureprofile.cs` | `CreatureProfile : JournalSubjectProfile` | เพิ่มข้อมูลเฉพาะสัตว์: Vision Cone (`viewAngle`, `viewRadius`), Awareness Radius, Obstacle Layer, ความสูงตา, ตัวคูณ Stealth ตอนย่อ, ความเร็วเดิน/วิ่ง, ระยะหนี, เวลาที่ต้องเห็นก่อนตกใจ ฯลฯ — สร้างผ่าน `Create > Creature > Creature Profile` |
| `Plantprofile.cs` | `PlantProfile : JournalSubjectProfile` | **ไม่มี field เพิ่มเลย** เพราะพืชไม่ขยับ/ไม่มองเห็นผู้เล่น — ใช้แค่ Identity จาก Base Class |
| `Photosubject.cs` | `PhotoSubject` | แปะที่ Root Object ของสิ่งที่ถ่ายรูปได้ ลาก `JournalSubjectProfile` (จะเป็น Creature หรือ Plant ก็ได้) มาใส่ `profile` แล้ว `CreatureId` จะดึงจาก Asset นั้นอัตโนมัติ (กันพิมพ์ผิด) |

**ข้อมูล Asset จริงตอนนี้** (`Assets/[02]Code/Data/Profile/`): CreatureProfile 1 ตัว (Deer) และ PlantProfile 4 ตัว (Chan/FlyAgaric/Honey/Turkey)

### `Creature/AI/` — พฤติกรรมสัตว์ (เฉพาะ CreatureProfile เท่านั้น พืชไม่มี AI)

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Behaviortree.cs` | `BTNode` (abstract), `BTSelector`, `BTSequence`, `BTCondition`, `BTAction`, enum `BTStatus` | Framework Behavior Tree แบบเบาที่สุด เขียนเป็นโค้ดตรงๆ ไม่มี Visual Editor — `BTSelector` = OR ตามลำดับความสำคัญ, `BTSequence` = AND |
| `Creatureai.cs` | `CreatureAI` (ต้องมี `NavMeshAgent` + `CreatureVision`) | สมองของสัตว์ ใช้ `CreatureProfile` เป็นค่าปรับแต่งทั้งหมด (ไม่มี Field ของตัวเอง) โครงสร้าง: Root Selector → **Engage branch** (ผู้เล่นเข้า Awareness Radius → วิ่งหนีทันที / เห็นในโคนสายตาต่อเนื่องครบเวลา → Alert แล้วค่อย Run) → **Normal branch** (Idle ↔ Walking สุ่มไปมาในรัศมี `wanderRadius`) State: `Idle, Walking, Alert, Run` ยิง `OnStateChanged` ให้ Animator ฟัง |
| `Creaturevision.cs` | `CreatureVision` | ตรวจจับผู้เล่น 2 แบบ: **Vision Cone** (ต้องอยู่ในมุม/ระยะ + ไม่มีอะไรบัง (`Physics.Linecast`), หดแคบลงอัตโนมัติเมื่อผู้เล่นย่อ) และ **Awareness Radius** (รอบตัว ตรวจจับได้ทุกทิศทาง ไม่ลดตาม Crouch) |

**ที่ยังไม่ได้ทำ (ระบุไว้ในคอมเมนต์โค้ดเอง):** Time Cycle (แยกพฤติกรรม Normal/Engage ตามช่วงเวลาในเกม) — ตกลงกันไว้แล้วแต่ Root ยังไม่มี Gate นี้

---

## 5. Journal — สมุดบันทึก

โฟลเดอร์ `Assets/[02]Code/Script/Journal/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Journalentry.cs` | `JournalEntry : ScriptableObject` | ข้อมูล 1 รายการในสมุด — `creatureId`, `category` (Plant/Animal), `displayName`, `description` (โชว์ต่อเมื่อมีรูป **และ** NPC เคยเล่าเรื่องนี้ให้ฟังแล้วเท่านั้น — ดู `QuestManager.IsInformed`), `habitatInfo` (โชว์เสมอ ช่วยหาตัว), `referenceImage` (⚠️ ยังไม่มีสคริปต์ไหนอ่านค่านี้), `silhouette` (เงาดำ โชว์ตลอดจนกว่าจะมีรูป) |
| `Journalmanager.cs` | `JournalManager` | เก็บ `List<JournalEntry> allEntries` (ตั้งจาก Inspector) เป็นตัวกลางจับคู่กับภาพจาก `PhotoStorage` ผ่าน `creatureId` — Subscribe `PhotoStorage.OnPhotoAdded` แล้ว **ทำสำเนา Texture2D ถาวรเก็บเองทันทีที่ปลดล็อกครั้งแรก** (ไม่อ้างอิงไฟล์ใน Storage อีกเลย ต่อให้ลบภาพต้นฉบับทีหลัง Journal ก็ไม่หาย) ไม่ persist ข้าม Scene |
| `Journalui.cs` | `JournalUI` | ตัวคุม UI ทั้งหมด แบ่ง 3 ชั้น: **Category** (เลือกพืช/สัตว์) → **Grid** (9 ช่อง/หน้า พร้อม Pagination, แต่ละช่องโชว์ชื่อใต้รูป) → **Detail** (ชื่อ+ที่อยู่โชว์เสมอ, รูปโชว์เมื่อมีรูปแล้ว, **คำอธิบายโชว์ต่อเมื่อมีรูปแล้วและ NPC เคยเล่าเรื่องนี้ให้ฟังแล้วเท่านั้น** — เช็คผ่าน `QuestManager.IsInformed`) ปุ่ม Toggle เปิด/ปิดทั้งชุด, ปุ่ม Cancel (Esc) ถอยทีละชั้น ผูก `SystemState.Journal` ระหว่างเปิดอยู่ Subscribe `QuestManager.OnQuestStatusChanged` เพื่อรีเฟรชจุดแดง Quest บนกริดแบบ Real-time ครั้งแรกที่เปิดหน้า Detail ของ Entry ที่มีรูปแล้ว รูปจะเฟดจาก Silhouette ไปเป็นรูปที่ถ่ายจริง (Coroutine `RevealPhotoFade`, ปรับเวลาได้ที่ `photoRevealFadeDuration`) — เปิดดูซ้ำครั้งต่อไปโชว์รูปจริงทันที (จำไว้ใน `revealedCreatureIds`, เป็น session-only ไม่ persist ข้าม Scene) |
| `Journalgridslotui.cs` | `JournalGridSlotUI` | ช่อง 1 ช่องในกริด — โชว์ Silhouette + ชื่อ (`displayName`) ใต้รูปเสมอ ไม่ว่าจะปลดล็อกหรือยัง + จุดแดงบอก Quest Active ตัดสินใจอะไรไม่ได้เองทั้งหมด แค่แจ้ง `JournalUI` ว่าถูกคลิก/เลือก |

---

## 6. Quest — เควสและ NPC

โฟลเดอร์ `Assets/[02]Code/Script/Quest/` (ใช้ Yarn Spinner)

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Questmanager.cs` | `QuestManager` (Singleton, `DontDestroyOnLoad`) | คุมสถานะเควสต่อ `creatureId` (`NotStarted → Active → Completed`) **และ** สถานะ "NPC เคยเล่าข้อมูลให้ฟังแล้วหรือยัง" ต่อ `creatureId` แยกกันคนละชุด (`informedCreatureIds`) เชื่อมกับ Yarn ผ่าน `[YarnCommand]`/`[YarnFunction]` โดยตรง ไม่ต้องเขียน Bridge เพิ่ม: `<<start_quest "id">>`, `<<complete_quest "id">>`, `<<mark_informed "id">>`, `<<if has_photo("id")>>`, `<<if quest_status("id") == "Active">>`, `<<if is_informed("id")>>` มี `ResetAllQuests()` ล้างทั้งสถานะเควสและความรู้ที่เคยเล่าไปแล้วสำหรับ Debug Reset และ auto re-hook `journalManager` reference ทุกครั้งที่ Scene โหลดใหม่ (แก้บั๊ก stale reference ตอน reload) |
| `Npcinteractable.cs` | `NPCInteractable` | ผู้เล่นเดินเข้าใกล้ (`interactRange`) แล้วกด Interact เพื่อเริ่ม Yarn Node (`startNode`) ผ่าน `DialogueRunner` ตั้ง `SystemState.Talking` ระหว่างคุย คืนเป็น `Normal` เองตอนจบบทสนทนา (ผูก `dialogueRunner.onDialogueComplete`) — โชว์/ซ่อน `interactPrompt` (UI เช่น "กด E เพื่อคุย") ตาม `isInRange` และ `SystemState == Normal` |
| `Questdebugui.cs` | `QuestDebugUI` | **เครื่องมือ Debug** — โชว์ข้อความ `Capture "ชื่อ"` มุมซ้ายบนจอสำหรับทุกเควสที่กำลัง `Active` อยู่ (สแกน `journalManager.allEntries` ทุกตัว) รีเฟรชอัตโนมัติทุกครั้งที่เควสเปลี่ยนสถานะ |

โฟลเดอร์ `Assets/[02]Code/Script/Dialogue/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `NPC_Placeholder.yarn` | — | เนื้อหา Dialogue จริง มีไฟล์เดียว 1 Node (`NPC_Start`) ผูกกับเควสเดียว (`deer_01`) เท่านั้น พืชอีก 4 ชนิดถ่ายได้แต่ไม่มีเควสผูกไว้ |
| `DialogueAnyKeyAdvance.cs` | `DialogueAnyKeyAdvance` (ต้องมี `Yarn.Unity.LineAdvancer` บน Object เดียวกัน) | **เครื่องมือ Playtest** — กดปุ่มไหนก็ได้บนคีย์บอร์ด (`Keyboard.anyKey`) หรือคลิกซ้ายก็เดินบทสนทนาต่อได้ (เรียก `LineAdvancer.OnInputHurryUpLines()` เหมือนกด Space) ⚠️ ยิงซ้ำกับปุ่ม Space ของ `LineAdvancer` เดิมในเฟรมเดียวกัน (Space ก็นับเป็น "any key" ด้วย) ไม่กระทบการใช้งานจริงมาก แต่ถ้ารำคาญให้ปิด/ลบ Component `LineAdvancerInput.KeyCodes` บน Dialogue System ออก |

---

## 7. Storage — คลังภาพ

โฟลเดอร์ `Assets/[02]Code/Script/Storage/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Photostorage.cs` | `PhotoStorage` (Singleton, `DontDestroyOnLoad`) | เก็บแค่ **Path ไฟล์ PNG บนดิสก์ + Metadata** (`creatureIds`, เวลาถ่าย) ไม่เก็บ Texture2D ค้าง RAM โหลดเป็น Texture2D เฉพาะตอนต้องการดูจริงผ่าน `LoadPhotoTexture()` จำกัดจำนวนสูงสุด (`maxCapacity`, ปกติ 30) ลบไฟล์ทั้งหมดอัตโนมัติตอนปิดเกม (`OnApplicationQuit`) มี `ClearAllPhotos()` สำหรับ Debug Reset |
| `Storageui.cs` | `StorageUI` | UI คลังภาพ แยกจาก Journal เต็มรูปแบบ — เปิด/ปิดด้วยปุ่ม Toggle (ผูก `SystemState.Storage`), สร้าง Thumbnail Grid จาก `PhotoStorage.Photos`, คลิกซ้ายขยายเต็มจอ/คลิกขวาเปิด Popup ยืนยันลบ, Smart Scroll เลื่อนจอตามช่องที่เลือกอัตโนมัติ (รองรับทั้งเมาส์และจอย/คีย์บอร์ด) |
| `Storagethumbnailui.cs` | `StorageThumbnailUI` | ช่อง 1 ช่องในกริด Storage รองรับทั้งเมาส์ (`OnPointerClick`) และจอย/คีย์บอร์ด (`OnSubmit`, `OnSelect`) ตัดสินใจอะไรไม่ได้เอง ส่งต่อให้ `StorageUI` ทั้งหมด |

---

## 8. Audio

โฟลเดอร์ `Assets/[02]Code/Script/Audio/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Audiomanager.cs` | `AudioManager` (Singleton, `DontDestroyOnLoad`) | ระบบเสียงกลาง — เรียก `AudioManager.Instance.PlaySFX(clip)` ได้จากทุกที่ ไม่ต้องมี `AudioSource` ของตัวเอง ใช้ Pool ของ `AudioSource` หมุนเวียนกัน (ค่าเริ่มต้น 8 ตัว) รองรับเสียงซ้อนกันหลายตัวพร้อมกัน มี `PlaySFXAtPoint()` สำหรับเสียง 3D ตำแหน่งในโลก **ใช้กับเสียง One-shot เท่านั้น** เสียง Loop ต่อเนื่อง (เช่นมอเตอร์ซูม) ต้องมี `AudioSource` แยกเอง |

---

## 9. Environment — สร้างสิ่งแวดล้อมจาก Terrain

โฟลเดอร์ `Assets/[02]Code/Script/EnviromentScript/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `InstantiateEnviromentObject.cs` | `InstantiateEnviromentObject` | สุ่มเลือก 1 จาก List Prefab แล้ว Instantiate ที่ตำแหน่งตัวเอง จากนั้น Destroy ตัวเองทิ้ง (ใช้เป็น "ตัวแทน" วางในฉากแล้วสุ่มเป็นของจริงตอนเริ่มเกม) |
| `TerrainTreeEnviromentSpawner.cs` | `TerrainTreeEnviromentSpawner` (ต้องมี `Terrain`) | ต้นไม้ที่ปลูกด้วยเครื่องมือ Paint Trees ของ Unity (หรือ VegetationSpawner) ไม่ได้เป็น GameObject จริง — Unity วาดจาก `TerrainData` เฉยๆ สคริปต์นี้วน `TreeInstance` ทั้งหมด แปลงตัวที่ Prototype มี `InstantiateEnviromentObject` ให้กลายเป็น GameObject จริงในตำแหน่ง/ขนาด/หมุนเดิม แล้วลบ Instance นั้นออกจาก TerrainData (กันวาดซ้อน) — ต้องทำแบบนี้เพื่อให้ต้นไม้ที่ถ่ายรูปได้ (มี Collider + PhotoSubject) ยังโชว์บน Terrain ได้ |

---

## 10. UI — HUD กลางจอ

โฟลเดอร์ `Assets/[02]Code/Script/UI/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `WorldHudUI.cs` | `WorldHudUI` | โชว์/ซ่อน `hudRoot` (ไอคอนกลาง + ปุ่ม Storage/Camera/Journal/Map รอบข้าง) ตาม `SystemState` — โชว์เฉพาะ `Normal` เท่านั้น ปิดทั้งก้อนทันทีที่เปิด Photograph/Journal/Storage/Talking/Pause (Pattern เดียวกับ `PhotoGridUI.cs`) ปุ่มแต่ละปุ่มผูก `Button.onClick` ตรงไปที่ `JournalUI.ToggleJournalUI()` / `StorageUI.ToggleStorageUI()` / `PhotoShooter.ToggleEnterPhotoMode()` เอาใน Inspector ไม่ผ่าน `WorldHudUI` เลย — ปุ่ม Map ยังไม่มีระบบ Map ให้เรียก (ดูหัวข้อ 12) |

---

## 11. Main Menu

โฟลเดอร์ `Assets/[02]Code/Script/MainMenu/` — Scene แยกต่างหาก (`MainMenu.unity`) ต้องอยู่ก่อน `Level_Prototype` ใน Build Settings เพราะ `DebugGameReset` โหลดกลับมาที่นี่ด้วยชื่อ Scene

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `MainMenuController.cs` | `MainMenuController` | `StartGame()` โหลด `gameSceneName` (ค่าเริ่มต้น `"Level_Prototype"`), `ExitGame()` ปิดเกม (`Application.Quit()`, หยุด Play Mode ใน Editor) — ผูกกับปุ่ม Start/Exit ผ่าน Button OnClick ใน Inspector เอง ไม่มี Logic อื่นแล้ว |

---

## 12. ข้อจำกัด/สิ่งที่ยังไม่มีตอนนี้

รายการนี้เป็น "สแนปช็อต" ณ วันที่อัปเดตเอกสาร ไม่ใช่ Bug Tracker ถาวร — ถ้าแก้แล้วให้ลบออกจากลิสต์นี้ทันที:

- `StateManager.MovementState.Running` ประกาศไว้ใน enum แต่ยังไม่มีใครสั่งใช้จริง (`PlayerSprint.cs` มี comment บอกไว้ว่ายังไม่ทำ) — วิ่งเร็วขึ้นจริงแต่ Animator อาจไม่เล่นท่าวิ่ง
- `StateManager.SystemState.Pause` มีประกาศและเดินสายไว้ใน `CanControlPlayer()`/`CanCrouch()` แล้ว แต่ไม่มี Pause Menu ใดๆ เรียกใช้จริง
- ไม่มี Tutorial/คำแนะนำผู้เล่นครั้งแรกใดๆ ในเกม
- `JournalEntry.referenceImage` มี field แต่ไม่มีสคริปต์ไหนอ่านค่านี้เลย
- Creature AI รองรับ Time Cycle (พฤติกรรมต่างกันตามช่วงเวลา) ไว้ในดีไซน์ แต่ยังไม่ implement ใน `Creatureai.cs`
- มี Creature ที่ตั้งค่า AI ไว้จริงแค่ 1 สายพันธุ์ (Deer) และมี Quest/Dialogue จริงแค่ 1 เควส ผูกกับ `deer_01` เท่านั้น — พืช 4 ชนิดถ่ายได้แต่ไม่มีเควส
- `InputManager.cs` เป็น dead code ไม่มีใครเรียกใช้
- ไม่มีระบบ Map เลย — ปุ่ม Map บน `WorldHudUI` ยังไม่มีอะไรให้เรียก (แค่ไอคอนเปล่า)
- `MainMenu.unity` ต้องสร้างเอง (Canvas + EventSystem + ปุ่ม Start/Exit ผูก `MainMenuController`) และเพิ่มเข้า Build Settings คู่กับ `Level_Prototype` — ยังไม่มีในโปรเจกต์จนกว่าจะสร้างใน Editor

---

## 13. กติกาการอัปเดตเอกสารนี้

**เอกสารนี้ต้องอัปเดตทุกครั้งที่:**
1. เพิ่มระบบ/สคริปต์ใหม่ที่มีผลต่อ Gameplay
2. แก้ไข/ลบระบบเดิมจนพฤติกรรมที่เขียนไว้ในนี้ไม่ตรงกับโค้ดจริงแล้ว
3. ผู้ใช้สั่งให้แก้ไขไฟล์นี้โดยตรง

อัปเดตทั้ง [README.md](README.md) (ย่อ) และไฟล์นี้ (ละเอียด) คู่กันเสมอ — ห้ามอัปเดตแค่ไฟล์เดียว
