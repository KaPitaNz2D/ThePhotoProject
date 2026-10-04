# The Photo Project — เอกสารระบบละเอียด

> เวอร์ชันย่ออ่านเร็วอยู่ที่ [README.md](README.md) — ไฟล์นี้คือรายละเอียดทางเทคนิคของทุกสคริปต์ใน `Assets/[02]Code/Script/`
>
> **อัปเดตล่าสุด:** 2026-10-03 (Unity 6000.3.19f1, Input System ใหม่, Yarn Spinner, Cinemachine 3)

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
| `PlayerMovement.cs` | `PlayerMovement` | เดิน/กระโดดผ่าน `Rigidbody`, เช็คพื้น (Raycast), รองรับทางลาด (Slope), จำกัดความเร็ว, รายงาน `MovementState` (Idle/Walking/Crouch) ให้ `StateManager` ทุกเฟรม — **ไม่มีแรงเสียดทาน:** Collider ของผู้เล่น (`PlayerObj`) ใช้ Physic Material `Assets/[02]Code/Data/Physics/PlayerNoFriction.asset` (แรงเสียดทาน 0, Combine = Minimum ซึ่งชนะ Average ของพื้น/Terrain) และใช้ `groundDrag` เป็นตัวหยุดตัวเอง **ทุกครั้งที่ติดพื้น (รวมพื้นเอียง)** เดิมพื้นเอียงไม่มี drag แต่พึ่งแรงเสียดทานเริ่มต้น 0.6 กับ `slopeStickForce` (30N) ที่กดตัวลงพื้น → แรงเสียดทานสูงสุด ≈ 18N มากกว่าแรงเดินตอนย่อ (`moveSpeed` x `crouchSpeedMultiplier` x 10) ทำให้ย่อแล้วเดินขึ้นทางเอียงไม่ได้ เดินปกติก็ขึ้นทางชันเกิน ~35° ไม่ได้ และเดินเฉียงชนกำแพงแล้วติด — ⚠️ ยังไม่มีระบบก้าวขึ้นขั้น (step up): ขอบ/ขั้นสูง ~0.3 หน่วยขึ้นไปยังขึ้นไม่ได้ (ต่ำกว่า ~0.2 ผ่านได้) |
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
| `CameraShoulderSwitch.cs` | `CameraShoulderSwitch` (ต้องมี `CinemachineRotationComposer`) | สลับกล้องไหล่ซ้าย/ขวา (Over-the-shoulder) ด้วยปุ่มเดียว (Q) — ทำงานเฉพาะตอน `CanControlPlayer()` (โหมดเดินปกติ) เท่านั้น เพราะ Q ถูกใช้เอียงกล้องตอนถ่ายรูปด้วย ไม่งั้นกดเอียงกล้องแล้วกล้องหลักที่ซ่อนอยู่จะสลับไหล่เงียบๆ (ส่วน E = คุย NPC `Interect` กันไว้ที่ `Normal` อยู่แล้ว ไม่ชน) |
| `Cameracontroller.cs` | `CameraController` | สลับ Priority ระหว่าง Vcam เดิน (Third Person) กับ Vcam ถ่ายรูป (Photo Cam) ตาม `StateManager.SystemState` — ไม่มี Logic เดิน/ถ่ายรูปเอง แค่ "ฟัง" state แล้วสลับกล้อง ตั้งทิศเริ่มต้นของกล้องถ่ายรูป (Pan/Tilt) ทุกครั้งก่อนเข้าโหมดถ่ายรูป (รายละเอียดด้านล่าง) และปิด `CinemachineInputAxisController` ของ Third Person Cam ทุกครั้งที่ `!CanControlPlayer()` (Photograph/Talking/Pause/Journal/Storage) กันกล้องหมุนค้างจาก Input เมาส์ที่ยังไหลเข้า Vcam แม้ Priority จะลดจนไม่เห็นภาพแล้ว — **ซ่อนตัวละครตอนถ่ายรูป:** field `characterModel` (ผูกกับ `PlayerObj` ใน `Player.prefab`/`Player_Code_Dev.prefab`) ตอนเข้าโหมดถ่ายรูป (ในจังหวะจอดำของ Fade) จะปิด `Renderer` ที่เปิดอยู่ทั้งหมดใต้โมเดล แล้วเปิดคืนเฉพาะตัวที่ปิดเอง ตอนออก (ไม่ไปเปิดตัวที่ตั้งใจปิดไว้) — (กล้องอยู่ติดหัวจะได้ไม่เห็นหัว/ไหล่ตัวเอง) — **ทิศเริ่มต้นและขอบเขตการหันของกล้องถ่ายรูป** (`photoCam` → `CinemachinePanTilt`, Pan อ้างอิง `PhotoCameraPivot` ซึ่งเป็นลูกของ `PlayerObj` เลยวัดจากทิศหน้าตัวละคร): ตอนเข้าโหมดถ่ายรูป (`AlignPhotoCamToThirdPersonView`) อ่านทิศแนวนอนที่ Third Person Cam มองอยู่แล้วแปลงเป็นมุม Pan เทียบกับหน้าตัวละคร → กล้องถ่ายรูป **เริ่มที่ทิศเดียวกับที่เพิ่งมองอยู่เสมอ** (ซ้าย/ขวา/ข้างหลังก็ได้ ไม่ฟิกที่หน้าตัวละคร) โดย **ตัวละครไม่ต้องหมุน** ส่วน Tilt เริ่มที่ 0 (ระดับสายตา เพราะ Third Person Cam มักก้มมองลง) — ช่วง Pan ของกล้องถ่ายรูปคือ **360°** (Range −180..180 + Wrap) ส่วน **Tilt จำกัด ±60°** (เดิม ±70°) — ความเร็วหันของกล้องถ่ายรูปคือ Gain ของ `CinemachineInputAxisController` บน `photoCam` (Pan = 2, Tilt = −2 เครื่องหมายลบคือกลับแกน Y) และความเร็วซูมคือ `PhotoZoom.zoomSpeed` = 40 / `fovSmoothing` = 15 (ปรับใน Prefab ได้) |
| `PhotoTransitionUI.cs` | `PhotoTransitionUI` | คุม Overlay จอดำ 2 แบบ: `PlayTransition()` (เฟดดำคู่ขนานตอนตัดกล้อง ไม่รอเฟดเสร็จก่อนตัด) และ `PlayShutterFlash()` (แฟลชสั้นๆ ตอนกดชัตเตอร์) |

### `Camera/Photograph/` — ระบบถ่ายรูปโดยเฉพาะ

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `PhotoShooter.cs` | `PhotoShooter` | หัวใจของระบบถ่ายรูป — จัดการเข้า/ออกโหมดถ่ายรูป, ยิง `SphereCastAll` ตรวจจับวัตถุ Tag `"Photographable"` ที่มี `PhotoSubject`, อัปเดตสี Crosshair Real-time, Capture ภาพผ่าน `RenderTexture` แล้วยิง Event `OnPhotoCaptured(Texture2D, List<GameObject>)`. ระยะตรวจจับ (`EffectiveCastDistance`) ยืดอัตโนมัติตามระดับซูมจาก `PhotoZoom`. **หมายเหตุ:** การตรวจจับเป็นแค่ Cone/Sphere ด้านหน้ากล้อง ไม่เช็คว่าเฟรมสวย/อยู่กลางจอไหม |
| `PhotoZoom.cs` | `PhotoZoom` | ปรับ FOV กล้องถ่ายรูปทั้งฝั่งแสดงผล (`photoVcam`) และฝั่ง Capture (`captureCamera`) ให้ตรงกันเสมอ มี Motion Blur ตอนกำลังซูม (ผ่าน Volume) และเสียงคลิกตอนชนขอบซูม ทำงานเฉพาะ `SystemState.Photograph` |
| `PhotoRoll.cs` | `PhotoRoll` (ติดที่ `PhotoCam` คู่กับ `PhotoZoom`) | เอียงกล้องถ่ายรูป (Roll / Dutch angle) ด้วยปุ่ม **Q = เอียงซ้าย / E = เอียงขวา** กดค้างเพื่อเอียงต่อเนื่อง (`rollSpeed` 60°/วินาที) ปล่อยแล้วค้างมุมไว้ จำกัด **±60°** (`maxRoll`) ทำงานเฉพาะ `SystemState.Photograph` เอียงผ่านค่า `Lens.Dutch` ของ `photoVcam` (Cinemachine: Dutch บวก = เอียงซ้าย เลยกลับเครื่องหมายให้ E เอียงขวา) — กล้องที่ Capture ภาพคือ Main Camera ตัวเดียวกับที่เห็น ภาพที่บันทึกจึงเอียงตรงกับจอ รีเซ็ตเป็นระดับตอน "เข้า" โหมดถ่ายรูปทุกครั้ง (ไม่รีเซ็ตตอนออก กันภาพกระชากระหว่าง Fade ดำ) ใช้ Input Action `Photograph/Roll` (แกน 1D Q/E) ใน `PlayerControls.inputactions` — `CurrentRoll` เอาไปทำ UI วัดระดับได้ |
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
| `CreatureGroundAlign.cs` | `CreatureGroundAlign` | เอียงโมเดลสัตว์ให้ตรงกับความชันพื้น — `NavMeshAgent` หมุนแค่แกน Yaw ไม่เอียงตาม Slope ให้เอง ทำให้ขึ้น/ลงเนินแล้วขาลอยหรือจมพื้น สคริปต์นี้ยิง Raycast ลงพื้นทุก `LateUpdate` (ยิงเฉพาะ Layer `Ground` ผ่าน `groundMask` ไม่ใช่ Everything — ไม่งั้นจะยิงชน `CapsuleCollider` ของตัวสัตว์เองแล้วได้ normal ชี้ขึ้นตรงๆ เสมอ) แล้วหมุน **เฉพาะ Transform โมเดลลูก** (`visualRoot`, ถ้าไม่ใส่จะหาจาก `Animator` ลูกให้เอง) ด้วย Slerp (`alignSpeed`) ไม่แตะ Root ที่ `NavMeshAgent` คุมอยู่ กัน path เพี้ยน — ติดอยู่บน `Deer_Testing Variant.prefab` แล้ว |

> **หมายเหตุ Deer prefab:** `NavMeshAgent.baseOffset` ของ `Deer.prefab` ตั้งเป็น `1.257` (วัดจากระยะ pivot → ปลายเท้าของโมเดลจริง ค่าเดิม 1.11 ทำให้ตัวจมพื้นเล็กน้อย) ถ้าเปลี่ยนโมเดล/ริกของกวางต้องวัดค่านี้ใหม่

**ข้อมูล Asset จริงตอนนี้** (`Assets/[02]Code/Data/Profile/`): CreatureProfile 1 ตัว (Deer) และ PlantProfile 5 ตัว (Chan/FlyAgaric/Honey/Turkey/Puffball) — ⚠️ Puffball ยังไม่ถูกใส่เข้าไปใน `JournalManager.allEntries` ของ Scene เลย ถ่ายรูปติดแล้วจะไม่ขึ้นใน Journal จนกว่าจะลาก `Journalentry/Puffball.asset` เข้าไปใส่ List ด้วยตัวเอง

### `Creature/AI/` — พฤติกรรมสัตว์ (เฉพาะ CreatureProfile เท่านั้น พืชไม่มี AI)

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Behaviortree.cs` | `BTNode` (abstract), `BTSelector`, `BTSequence`, `BTCondition`, `BTAction`, enum `BTStatus` | Framework Behavior Tree แบบเบาที่สุด เขียนเป็นโค้ดตรงๆ ไม่มี Visual Editor — `BTSelector` = OR ตามลำดับความสำคัญ, `BTSequence` = AND |
| `Creatureai.cs` | `CreatureAI` (ต้องมี `NavMeshAgent` + `CreatureVision`) | สมองของสัตว์ ใช้ `CreatureProfile` เป็นค่าปรับแต่งทั้งหมด (ไม่มี Field ของตัวเอง) โครงสร้าง Root Selector (ลองกิ่งบนก่อน): **Run** (กำลังหนี ทำต่อจนห่างเกิน `safeDistance`) → **Panic** (ผู้เล่นเข้ามาในระยะ `startleRadius` ไม่ว่าทำอะไรอยู่ หรือ Sprint อยู่ในระยะ `sprintPanicRadius` → Run ทันทีจากทุก State ทางลัดสำหรับเข้าหาจากด้านหลังที่ลำดับ Stop → Alert ไม่ทัน และกวางที่งงอยู่จะไม่มีวันเห็นคนที่ยืนประชิดด้านหลังเพราะหัวหันได้แค่ ±`lookAroundAngle` — ระยะทั้งสองคูณ `PerceptionMultiplier` ตอนก้มกินหญ้า) → **Alert** (เผชิญหน้า หันตัวหาผู้เล่น: ผู้เล่นเข้าโหมดกล้อง/ถ่ายรูป/Sprint/ได้ยินชัตเตอร์ หรือเดินเข้าใกล้กว่าระยะตอนเข้า Alert เกิน `alertApproachDistance` → Run, ผู้เล่นยืนนิ่ง (ความเร็ว < `playerStillSpeed`) สะสมครบ `alertStillTime` → Run (ขยับ/ถอยหลังอยู่เวลาหยุดนับแต่ไม่รีเซ็ต), ถอยห่างเกิน `alertRetreatDistance` → Stop) → **Confuse** (**Stop** นิ่งงงไม่หันหาผู้เล่น: เห็นผู้เล่นในโคนสายตา → Alert ทันที, ครบ `stopDuration` หรือผู้เล่นเดินเข้าใกล้เพิ่มเกิน `stopApproachDistance` หรือเข้าโหมดกล้อง → **Look around** หันหัวกวาดซ้าย-ขวา `lookAroundAngle` และทุก `lookAroundTurnInterval` วินาทีสุ่ม (`lookAroundTurnChance`) หมุนตัวไปทิศอื่น `lookAroundTurnMinAngle`–`MaxAngle` องศา เพื่อมองถึงด้านหลังได้ นาน `lookAroundDuration` ถ้าสะสมเห็นผู้เล่นครบ `lookAroundSpotTime` → Alert, หมดเวลาโดยไม่เจอ → ถ้าผู้เล่นยังอยู่ใน Awareness Radius กลับ Stop แล้ววนต่อไปเรื่อยๆ (ไม่กลับ Normal ไม่ว่ายืนนิ่งหรือเดินเข้ามา) ออกจากวงแล้วค่อยกลับ Idle) → **Triggers ของ Normal** (เห็นผู้เล่นในโคนสายตาครบ `visionDetectionTime` (ย่อ = ×`crouchDetectionTimeMultiplier`) → Alert; ได้ยินชัตเตอร์ในระยะ `shutterHearingRadius` (ทะลุสิ่งกีดขวางได้) หรือผู้เล่นเข้า Awareness Radius → Stop) → **Normal branch** (Idle: ยืนเฉยๆ หรือสุ่ม `idleLookChance` หันหัวมองซ้าย/ขวา/ทั้งสองข้าง; หมด Idle สุ่ม `eatChance` ไป **Eating** ถ้ายืนบนพื้นหญ้า (`GrazeSurface`) ก้มกิน `eatMinDuration`–`eatMaxDuration` วินาที ระหว่างกินการรับรู้ทั้งหมด (โคนสายตา/วงรอบตัว/ระยะได้ยิน) ×`eatPerceptionMultiplier` (0.5) ; ไม่งั้นเดิน Walking ไปมาระหว่าง **โหนดจาก `AINodeNetwork`** ในรัศมี `wanderRadius` รอบตำแหน่งปัจจุบันของตัวเอง (ไม่ยึดจุดเกิด กวางที่หนีไปไกลจึงไม่เดินกลับจุดเดิม) — เลือกโหนดที่ห่างจากตัวอย่างน้อย 3 หน่วย และเช็ค `CalculatePath` ว่า `PathComplete` ก่อนเดิน (ข้ามโหนดที่อยู่บนเกาะ NavMesh ที่ไปไม่ถึง ลองสุ่มได้ 8 ครั้ง) ถ้าไม่มี `AINodeNetwork` ในซีนหรือหาโหนดไม่ได้ กลับไปสุ่มจุดอิสระแบบเดิม; field `nodeNetwork` ไม่ใส่ก็ได้ จะหาในซีนให้เอง) State: `Idle, Walking, Alert, Run, Eating, Stop, LookAround` ยิง `OnStateChanged` ให้ Animator ฟัง (`CreatureAnimatorController` แม็พ Eating = 4, Stop/LookAround ใช้ท่า Idle) — ค่าผู้เล่นที่ AI ใช้: ความเร็วแนวราบจาก Rigidbody, `StateManager` (Sprint / โหมดถ่ายรูป / ย่อ) — ตอนวิ่งหนี (`UpdateFleeDestination`) สุ่มเบี่ยงมุมจากทิศตรงข้ามผู้เล่น ±`fleeAngleVariance` องศา (ค่าอยู่ใน `CreatureProfile`) กันวิ่งเป็นเส้นตรงเป๊ะทุกครั้ง — ตอนเดินเล่น (`UpdateWalking`) มี timeout `maxWalkDuration` กันเดินติดค้างถาวร (เช่นชนสิ่งกีดขวางที่ NavMesh ไม่ได้กันไว้) ถ้าเดินไม่ถึงจุดหมายภายในเวลานี้จะยกเลิกแล้วกลับ Idle เอง — ระยะเวลาเดินปกติต่อรอบคุมผ่าน `wanderRadius` (ยิ่งกว้างยิ่งเดินนาน) ไม่ใช่ตัวจับเวลาตรงๆ |
| `Creaturevision.cs` | `CreatureVision` | ตรวจจับผู้เล่น 2 แบบ: **Vision Cone** (ต้องอยู่ในมุม/ระยะ + ไม่มีอะไรบัง (`Physics.Linecast`), หดแคบลงอัตโนมัติเมื่อผู้เล่นย่อ) และ **Awareness Radius** (รอบตัว ตรวจจับได้ทุกทิศทาง ไม่ลดตาม Crouch) — ตอนนี้ Awareness Radius คือ "วงที่ทำให้กวางงง (Stop)" ไม่ใช่วงวิ่งหนีทันทีแล้ว — โคนสายตาหมุนตามหัวที่หันอยู่ (`CreatureHead.CurrentYaw` ผ่าน `GetLookForward()`) และทุกขอบเขตคูณ `PerceptionMultiplier` (0.5 ตอนก้มกินหญ้า) |
| `CreatureHead.cs` | `CreatureHead` (ติดที่ราก Prefab สัตว์) | หันเฉพาะหัวด้วยโค้ด ไม่ใช้ Animation: บิดกระดูก `head.x` เต็มมุม + `neck.x` สัดส่วน `neckShare` ใน `LateUpdate` ทับท่าจาก Animator รอบแกนตั้งของโมเดลในพิกัดโลก (ไม่ขึ้นกับแกนกระดูกของ rig) — `LookYaw(yaw)`, `ResetLook()`, `PlaySequence(yaws, holdTime, loop)` (หันไปทีละมุมแล้วค้าง) — หาชื่อกระดูกเองถ้าไม่ใส่ (สายพันธุ์ใหม่ชื่อต่างต้องใส่ช่อง Head Bone/Neck Bone) ตอนนี้ติดที่ `Deer_Testing Variant.prefab` |
| `CreatureDebug.cs` | `CreatureDebug` (ติดที่ราก Prefab สัตว์ ตอนนี้ติดที่ `Deer_Testing Variant.prefab`) | เครื่องมือ Debug: ป้ายลอยเหนือหัวใน Game view (State สีตามสถานะ, ระยะถึงผู้เล่น, อยู่ในวง Stop/Panic/หูชัตเตอร์หรือไม่, เห็นผู้เล่นในโคนสายตาไหม, ตัวจับเวลาของ State นั้น เช่น ผู้เล่นนิ่งสะสม x/5s), วงระยะบนพื้นผ่าน Gizmos (แดง = วงงง, ฟ้า = หูชัตเตอร์, ส้ม = Sprint Panic, เหลือง = Startle ประชิดตัว, เขียว = Safe Distance; ตอน Alert เพิ่มวงขาว = ระยะตอนเข้า, แดงเข้ม = ระยะเข้าใกล้แล้วหนี, น้ำเงิน = ระยะถอยแล้วกลับ Stop) + เส้นม่วงถึงผู้เล่น, และ Log ทุกครั้งที่เปลี่ยน State — ปิดแต่ละส่วนได้จาก Inspector (`showLabel`, `showRanges`, `logStateChanges`) ถอด Component ออกตอนปล่อยเกมจริง |
| `CreatureNoise.cs` | `CreatureNoise` (static) | ช่องทางประกาศเสียงให้สัตว์ได้ยิน ตอนนี้มี `EmitShutter(position)` ที่ `PhotoShooter.TakePhoto` เรียกทุกครั้งที่ถ่าย — สัตว์แต่ละตัว Subscribe เองแล้วเช็คระยะตาม `shutterHearingRadius` ของสายพันธุ์ (ไม่ต้องมี LOS) |
| `GrazeSurface.cs` | `GrazeSurface` (static) | `IsGrass(position, keyword, minCoverage)` อ่าน Alphamap ของ Terrain ที่จุดนั้น รวมสัดส่วนของ Terrain Layer ที่ชื่อมีคำ `grazeSurfaceKeyword` (ค่าเริ่ม "Grass" ซีนทดสอบมี `Grass_normal_down`, `Grass_swamp_lighted_up`) ถึง `grazeMinCoverage` ถึงนับว่ามีหญ้า — ซีนไม่มี Terrain = กินไม่ได้ |
| `AINodeNetwork.cs` | `AINodeNetwork` | เครือข่าย "โหนด" จุดเดินของ AI สร้างอัตโนมัติจาก NavMesh ที่ Bake ไว้แล้ว: กวาดกริดทั่ว Terrain ทุก `nodeSpacing` หน่วย แล้วเช็คแต่ละจุดด้วย `NavMesh.SamplePosition` (รัศมี `sampleRadius`) จุดไหนไม่ตก NavMesh (ชันเกิน/ใต้น้ำที่กันไว้/โดนสิ่งกีดขวางบัง) ถูกข้ามอัตโนมัติ เก็บเป็น `List<Vector3>` ไม่ใช่ GameObject (เบา ไม่มี Transform ต่อจุด) วาด Gizmo วงกลมฟ้าใน Scene view เท่านั้น — ฟังก์ชันให้ AI เรียก: `GetRandomNode()`, `TryGetRandomNodeNear(origin, radius, out node)` (คืน false ถ้าไม่มีโหนดในรัศมี ไม่ fallback ไปทั้งแมพ) — `CreatureAI` ใช้โหนดเหล่านี้เป็นจุดหมายตอน Walking แล้ว (ดูแถว `Creatureai.cs`) แบบ Lethal Company |
| `Spawn/AISpawnZone.cs` | `AISpawnZone` | วงกลมบนระนาบ XZ (ปรับ `radius` ได้ ไม่สนความสูง) กำหนดทั้ง **พื้นที่ที่ Spawn ได้** (โหนดที่อยู่ในวงเท่านั้นถึงนับ) และ **สัตว์ที่เกิดในพื้นที่นั้น** — ช่อง `Creatures` ลาก Prefab สัตว์ใส่ได้เลย แต่ละรายการมี `Weight` (น้ำหนักสุ่ม เช่น 3 กับ 1 = ชนิดแรกเกิดบ่อยกว่า 3 เท่า, 0 = ปิดชั่วคราว) โซนที่ไม่มีสัตว์ที่ใช้งานได้สักตัวจะถูกข้าม (Gizmo เป็นสีเทา ส่วนโซนที่ใช้งานได้เป็นสีส้ม) — โซนซ้อนทับกันใช้โซนแรกที่เจอ |
| `Spawn/CreatureSpawner.cs` | `CreatureSpawner` | Spawn/Despawn สัตว์บนโหนดของ `AINodeNetwork` — ใช้ `AISpawnZone` **ทุกอันที่มีในซีนอัตโนมัติ** (ไม่มีลิสต์ให้ลากเอง) ชนิดสัตว์มาจากรายชื่อสัตว์ของโซนที่โหนดนั้นอยู่ — **Spawn** ทุก `spawnInterval` วินาทีถ้ายังไม่ครบ `maxAlive` (นับรวมทุกชนิด): **โซนจะใช้ Spawn ได้เมื่อผู้เล่นยืนอยู่ในวงของโซนนั้นเท่านั้น** (ผู้เล่นนอกโซน = สัตว์ของโซนนั้นไม่เกิด) → คัดโหนดที่อยู่ในโซนเดียวกันนั้นและห่างผู้เล่น ≥ `minSpawnDistance` (ต้องน้อยกว่ารัศมีโซนพอสมควร ไม่งั้นตอนผู้เล่นยืนกลางโซนจะไม่เหลือโหนดไกลพอเลย) แล้วสุ่มจนเจอโหนดที่ **อยู่นอกสายตาผู้เล่น** (ลอง `spawnAttempts` ครั้ง) → สุ่มชนิดสัตว์ตามน้ำหนักของโซนนั้น → วางสัตว์ (ยกตาม `baseOffset` ของ NavMeshAgent) + ยิง `OnCreatureSpawned` + `Debug.Log` "(เสียงเตือน)" (ยังไม่มีเสียงจริง ให้ระบบเสียง Subscribe event นี้ภายหลัง) — **Despawn** (เช็คทุก 0.5 วิ): ต้องเคยเข้า state `Run` มาแล้ว + ไกลจากผู้เล่นเกิน `despawnDistance` + ไม่อยู่ในสายตา ต่อเนื่องครบ `despawnDelay` วินาที (ตัวนับรีเซ็ตเป็น 0 ทันทีที่ผู้เล่นเข้าใกล้หรือมองเห็น) — **"อยู่ในสายตา"** = ภายใน `maxVisibleDistance` + อยู่ใน frustum ของ `Camera.main` (เผื่อขอบ `viewportMargin`) + ไม่มี Collider ใน `occlusionMask` (ค่าเริ่มต้น Layer `Ground`) บังระหว่างกล้องกับตัวสัตว์ — คุมเฉพาะสัตว์ที่ Spawner นี้สร้างเอง ตัวที่วางไว้ในซีนไม่ถูกนับ/ไม่ถูก Despawn |
| `Editor/AINodeNetworkEditor.cs` | `AINodeNetworkEditor` (Editor-only) | Custom Inspector ของ `AINodeNetwork` เพิ่มปุ่ม **Generate Nodes** + แสดงจำนวนโหนดปัจจุบัน (กดสร้างใหม่ได้ทุกครั้งที่ Bake NavMesh ใหม่ หรืออยากเปลี่ยนระยะห่าง) |

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
| `Audiomanager.cs` | `AudioManager` (Singleton, `DontDestroyOnLoad`) | ระบบเสียงกลาง — เรียก `AudioManager.Instance.PlaySFX(clip)` ได้จากทุกที่ ไม่ต้องมี `AudioSource` ของตัวเอง ใช้ Pool ของ `AudioSource` หมุนเวียนกัน (ค่าเริ่มต้น 8 ตัว) รองรับเสียงซ้อนกันหลายตัวพร้อมกัน มี `PlaySFXAtPoint()` สำหรับเสียง 3D ตำแหน่งในโลก **ใช้กับเสียง One-shot เท่านั้น** เสียง Loop ต่อเนื่อง (BGM/Ambient เป็นต้น) ต้องมี `AudioSource` แยกเอง (Loop + Play On Awake ตั้งใน Inspector ตรงๆ ไม่ต้องพึ่งสคริปต์) |
| `DelayedAudioPlay.cs` | `DelayedAudioPlay` (ต้องมี `AudioSource`) | เล่น `AudioSource` บน Object เดียวกันวนซ้ำแบบมีช่วงเว้นระหว่างรอบ — หน่วง `delaySeconds` ก่อนเล่นครั้งแรก แล้วเว้น `gapBetweenLoops` ทุกครั้งที่เล่นจบก่อนเริ่มรอบใหม่ (ปิด `Loop` ของ `AudioSource` ให้อัตโนมัติ คุมจังหวะเองทั้งหมดด้วย Coroutine) ใช้กับเพลง BGM ที่ไม่อยากให้ต่อกันทันทีเหมือน Loop ปกติ ต้องปิด `Play On Awake` ของ `AudioSource` นั้นไว้ก่อน ไม่งั้นจะเล่นซ้ำสองรอบ |

---

## 9. Environment — สร้างสิ่งแวดล้อมจาก Terrain

โฟลเดอร์ `Assets/[02]Code/Script/EnviromentScript/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `InstantiateEnviromentObject.cs` | `InstantiateEnviromentObject` | สุ่มเลือก 1 จาก List Prefab แล้ว Instantiate ที่ตำแหน่งตัวเอง จากนั้น Destroy ตัวเองทิ้ง (ใช้เป็น "ตัวแทน" วางในฉากแล้วสุ่มเป็นของจริงตอนเริ่มเกม) |
| `TerrainTreeEnviromentSpawner.cs` | `TerrainTreeEnviromentSpawner` (ต้องมี `Terrain`) | ต้นไม้ที่ปลูกด้วยเครื่องมือ Paint Trees ของ Unity (หรือ VegetationSpawner) ไม่ได้เป็น GameObject จริง — Unity วาดจาก `TerrainData` เฉยๆ สคริปต์นี้วน `TreeInstance` ทั้งหมด แปลงตัวที่ Prototype มี `InstantiateEnviromentObject` ให้กลายเป็น GameObject จริงในตำแหน่ง/ขนาด/หมุนเดิม แล้วลบ Instance นั้นออกจาก TerrainData (กันวาดซ้อน) — ต้องทำแบบนี้เพื่อให้ต้นไม้ที่ถ่ายรูปได้ (มี Collider + PhotoSubject) ยังโชว์บน Terrain ได้ |
| `Editor/NavMeshTestEnvironmentGenerator.cs` | `NavMeshTestEnvironmentGenerator` (Editor-only) | เมนู `Tools > Photo Project > Generate NavMesh Test Environment` — สร้างสนามทดสอบ NavMesh แบบกดปุ่มเดียวโดยลบของเก่าก่อนสร้างใหม่ทุกครั้ง: **Terrain จริง 90x90 หน่วย** (ลานกลางเรียบ รัศมี ~16 + เนิน/ภูเขา 4 ลูกล้อมรอบ ความชันไล่ระดับ ~17°/~24°/~35°/~50° อ้างอิง `agentSlope` 48.3° ของ Deer — ลูกสุดท้ายตั้งใจให้ชันเกินลิมิตเพื่อทดสอบยอดที่เดินไม่ถึง) + **ต้นไม้ 55 ต้น** สุ่มกระจาย (ใช้ tree impostor ใน `Assets/TreeImpostors` เติม `CapsuleCollider` ที่โคนต้นให้กีดขวาง NavMesh เพราะ prefab ต้นไม้ไม่มี collider มาเอง) + วาง `Deer_Testing Variant` กับ `Player` ไว้บนลานกลาง แล้ว Bake NavMesh (legacy `NavMeshBuilder`) ให้อัตโนมัติ — TerrainData เซฟเป็น asset ที่ `Assets/[04]Level/TestingLab/AI_Walking_Test_Navmesh/` ใช้ในซีน `AI_Walking_Test_Navmesh` ไม่รวมอยู่ใน Build จริง |

> **การตั้งค่า NavMesh ของซีนจริง (`TestNavmesh.unity` / `PlaytestLevel_1.unity`):** ใช้ `NavMeshSurface` ที่ GameObject `Playtest_Terrain_Vegetation` (agent type ของ Deer) โหมดเก็บ geometry เป็น **Render Meshes** ทั้งซีน — เคยทำให้กวางเดินติดๆขัดๆ เพราะพืชที่ `VegetationSpawner` ปลูก (~1,100 ชิ้น ไม่มี Collider) ถูกเก็บเป็นสิ่งกีดขวาง NavMesh ทั้งที่ Player เดินทะลุได้ แก้โดยเพิ่ม `NavMeshModifier` (`ignoreFromBuild = true`) ที่ container พืชทั้ง 10 กลุ่ม (NavMesh triangle ลด ~90%) และกันพื้นใต้น้ำด้วย `NavMeshModifierVolume` (Area = Not Walkable) — ⚠️ ขนาดของ `NavMeshModifierVolume` เป็นคนละ field กับ `BoxCollider` ปรับ Collider แล้ว Volume ไม่ตามเอง ต้องตั้ง `size/center` ของ Volume แยกด้วย
| `TimeManager.cs` | `TimeManager` | วงจรกลางวัน-กลางคืน — ไล่ `Hours`/`Minutes` ตาม `Time Scale` ทุกเฟรม แล้วเปลี่ยน Skybox/สี Light/Fog ตาม `Time Periods` (array เรียงจากชั่วโมงน้อยไปมาก) พร้อมหมุนดวงอาทิตย์ (`UpdateSunRotation`) และ Lerp สีตอนเปลี่ยนช่วงเวลา เริ่มต้นที่ `Start Hour` (ตั้งค่าได้ใน Inspector) **ล็อคเวลาไว้ช่วงใดช่วงหนึ่งตลอด (เช่นกลางวัน):** ตั้ง `Start Hour` เป็นชั่วโมงที่ต้องการ แล้วตั้ง `Time Scale = 0` กันไม่ให้เวลาเดินต่อ |

---

## 10. UI — HUD กลางจอ

โฟลเดอร์ `Assets/[02]Code/Script/UI/`

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `Billboard.cs` | `Billboard` | หมุน Transform ให้หันตาม Rotation ของกล้องทุกเฟรม (`LateUpdate`) — ใช้กับ UI World Space เช่นป้าย "กด E" เหนือหัว NPC ไม่ใส่ `Target Camera` ไว้จะใช้ `Camera.main` ให้เอง |
| `WorldHudUI.cs` | `WorldHudUI` | โชว์/ซ่อน `hudRoot` (ไอคอนกลาง + ปุ่ม Storage/Camera/Journal/Map รอบข้าง) ตาม `SystemState` — โชว์เฉพาะ `Normal` เท่านั้น ปิดทั้งก้อนทันทีที่เปิด Photograph/Journal/Storage/Talking/Pause (Pattern เดียวกับ `PhotoGridUI.cs`) ปุ่มแต่ละปุ่มผูก `Button.onClick` ตรงไปที่ `JournalUI.ToggleJournalUI()` / `StorageUI.ToggleStorageUI()` / `PhotoShooter.ToggleEnterPhotoMode()` เอาใน Inspector ไม่ผ่าน `WorldHudUI` เลย — ปุ่ม Map ยังไม่มีระบบ Map ให้เรียก (ดูหัวข้อ 12) |

---

## 11. Main Menu

โฟลเดอร์ `Assets/[02]Code/Script/MainMenu/` — Scene แยกต่างหาก (`MainMenu.unity`) ต้องอยู่ก่อน `Level_Prototype` ใน Build Settings เพราะ `DebugGameReset` โหลดกลับมาที่นี่ด้วยชื่อ Scene

| ไฟล์ | Class | หน้าที่ |
|---|---|---|
| `MainMenuController.cs` | `MainMenuController` | `StartGame()` โหลด `gameSceneName` (ค่าเริ่มต้น `"Level_Prototype"`), `ExitGame()` ปิดเกม (`Application.Quit()`, หยุด Play Mode ใน Editor) — ผูกกับปุ่ม Start/Exit ผ่าน Button OnClick ใน Inspector เอง ไม่มี Logic อื่นแล้ว |

### Prefab หลักที่ใช้ซ้ำข้ามซีน (`Assets/[03]Prefabs/`)

ซีนใหม่ลาก 3 อันนี้ลงไปก็ได้ของครบ ไม่ต้องไล่ใส่ทีละชิ้นเหมือนเดิม:

| Prefab | ข้างใน | หมายเหตุ |
|---|---|---|
| `PlayerRig.prefab` | `Player_Code_Dev` (nested prefab — มี `StateManager`, `CameraController`, `PhotoShooter`, `PhotoStorage`, `JournalManager` และ UI ผ่าน `GameUIManager`) + `ThirdPersonCam` (nested prefab, Cinemachine) + `Main Camera` (วัตถุธรรมดา, มี `CinemachineBrain`) + `Icon UI` (nested prefab, World HUD) | Root ว่างอยู่ที่ตำแหน่งเกิดของผู้เล่น ย้าย Root เพื่อเปลี่ยนจุดเกิด กล้องอยู่เป็นพี่น้องกับ Player ใต้ Root เดียวกัน **ห้ามย้ายกล้องไปเป็นลูกของ Player** ไม่งั้นกล้องจะหมุนตามตัวละคร |
| `GameSystems.prefab` | `TimeManager` + `SoundController` + `Dialogue System` (nested จาก Yarn Spinner) + `DebugGameReset` | ของที่ต้องมีทุกซีนแต่ไม่ผูกกับแมพ |
| `AudioManager.prefab` | `AudioManager` ตัวเดียว | **ต้องอยู่เป็น Root ของซีนเอง** ไม่ซ้อนใต้ `GameSystems` เพราะเรียก `DontDestroyOnLoad(gameObject)` ซึ่งใช้ได้เฉพาะ Root เท่านั้น |

ของที่ยังอยู่ในซีนเอง (เฉพาะแมพ): Terrain/พืช (`Playtest_Terrain_Vegetation`), กระท่อม, `NPC`, `BlockPlayer`, `AI Node Network`, `AISpawnZone`, `CreatureSpawner`, กวางที่วางไว้ — `PlayerRig` สร้างจาก `Player_Code_Dev` ถ้าจะใช้ `Player.prefab` ตัวปกติ ให้ทำ Rig แยกอีกอัน

**ขนาด UI:** Canvas แบบ Screen Space ทั้งหมดในเกม (`Player.prefab` → `Player_Code_Dev` รับต่อ, `Icon UI.prefab`, `Debug-Quest` ใน `NPC.prefab`, และ Dialogue System) ใช้ `CanvasScaler` แบบ **Scale With Screen Size อ้างอิง 1920×1080** (Match 0.5; Dialogue System ใช้ Match 0) UI จึงย่อ/ขยายตามขนาดหน้าจอ/หน้าต่าง Game view อัตโนมัติ — เดิมเป็น Constant Pixel Size ทำให้ UI เพี้ยนทันทีที่ Game view ไม่ใช่ 1920×1080 ส่วน Canvas ที่เป็น World Space (เช่นของ NPC) ไม่แตะ

---

## 12. ข้อจำกัด/สิ่งที่ยังไม่มีตอนนี้

รายการนี้เป็น "สแนปช็อต" ณ วันที่อัปเดตเอกสาร ไม่ใช่ Bug Tracker ถาวร — ถ้าแก้แล้วให้ลบออกจากลิสต์นี้ทันที:

- `StateManager.MovementState.Running` ประกาศไว้ใน enum แต่ยังไม่มีใครสั่งใช้จริง (`PlayerSprint.cs` มี comment บอกไว้ว่ายังไม่ทำ) — วิ่งเร็วขึ้นจริงแต่ Animator อาจไม่เล่นท่าวิ่ง
- `StateManager.SystemState.Pause` มีประกาศและเดินสายไว้ใน `CanControlPlayer()`/`CanCrouch()` แล้ว แต่ไม่มี Pause Menu ใดๆ เรียกใช้จริง
- ไม่มี Tutorial/คำแนะนำผู้เล่นครั้งแรกใดๆ ในเกม
- `JournalEntry.referenceImage` มี field แต่ไม่มีสคริปต์ไหนอ่านค่านี้เลย
- Creature AI รองรับ Time Cycle (พฤติกรรมต่างกันตามช่วงเวลา) ไว้ในดีไซน์ แต่ยังไม่ implement ใน `Creatureai.cs`
- Stop / Look around / หันมองข้างๆ ยังไม่มี Clip เฉพาะ ใช้ท่า Idle (Clip เดียวเฟรมเดียว) + หันหัวด้วยโค้ด; ท่า Eating ใช้ Clip `Deer_Init_Eating` (ไม่วนซ้ำ ก้มหัวแล้วค้างท่าสุดท้าย); ท่า Alert ใช้ท่า Idle (State 0) เพราะ State 2 ใน Animator ผูก Clip เดินไว้ชั่วคราว ทำให้กวางเดินอยู่กับที่ตอนจ้องผู้เล่น — พอมี Clip Alert จริงให้แก้ `MapToAnimatorState` กลับเป็น 2
- Alert ที่ผู้เล่นถอยห่างเกิน `alertRetreatDistance` → Stop แต่ถ้าผู้เล่นยังอยู่ในโคนสายตา Stop จะส่งกลับ Alert ทันที (ระยะอ้างอิงถูกตั้งใหม่) — ถ้าอยากให้ Stop ค้างจริงต้องแก้ว่า Stop ที่มาจาก Alert ไม่เช็คสายตาช่วงแรก
- `safeDistance` ต้องมากกว่า `viewRadius` เสมอ (Deer.asset ตั้ง 60 กับ 50) ไม่งั้นกวางเห็นผู้เล่นไกลเกินระยะวิ่งหนี -> Alert -> Run แล้วหยุดทันทีเพราะ "ปลอดภัยแล้ว" -> เห็นอีก วนเป็นลูป
- `CreatureSpawner`: `despawnDistance` ต้องไม่มากกว่า `safeDistance` ใน `CreatureProfile` มากนัก (Deer = 30) เพราะสัตว์หยุดวิ่งหนีเมื่อห่างครบ `safeDistance` แล้วกลับไปเดินเล่นรอบจุดที่หยุด ถ้าตั้งไกลกว่านั้นจะไม่เคย Despawn; สัตว์ที่ไม่เคยวิ่งหนีเลยจะอยู่ตลอดและนับเต็มโควตา `maxAlive` ตามกติกาที่ตั้งไว้ — ยังไม่มีเสียงเตือนจริง (แค่ `Debug.Log` + event) และยังไม่มีกันไม่ให้ Despawn ตัวที่ผู้เล่นกำลังเล็ง/ถ่ายรูป (นอกจากเช็คสายตาทั่วไป)
- `PhotoStorage` (อยู่ใน `Player_Code_Dev/GameUIManager/StoragePanel`) และ `QuestManager` (อยู่ใน `NPC.prefab`) เรียก `DontDestroyOnLoad(gameObject)` ทั้งที่ไม่ใช่ Root → ขึ้น error "DontDestroyOnLoad only works for root GameObjects" ทุกครั้งที่เข้า Play และไม่ข้ามซีนจริง; `QuestManager` ยังผูกอยู่กับ `NPC.prefab` ทำให้ซีนที่ไม่มี NPC จะไม่มีระบบเควสเลย — ควรแยกเป็น Root ของตัวเอง (เช่นยกเข้า `AudioManager`-style prefab) แต่ยังไม่ได้ทำ
- `MainMenu.unity` และซีนเก่า `TestingLab/TestingLab 1.unity` ยังมี Canvas แบบ Constant Pixel Size อยู่ (ไม่ได้เปลี่ยนเพราะไม่รู้ความละเอียดที่ออกแบบไว้ของ MainMenu และ TestingLab 1 เป็นซีนทดสอบเก่า)
- `CreatureAI` เดินตามโหนดเฉพาะตอน Walking เท่านั้น ถ้าซีนไม่มี `AINodeNetwork` หรือหาโหนดที่เดินถึงไม่ได้ จะ fallback ไปสุ่มจุดแบบเดิม (`GetRandomPointInRadius`) — โหนดไม่มีเส้นเชื่อมระหว่างกัน (ไม่ใช่กราฟ) การหาเส้นทางและหลบสิ่งกีดขวางระหว่างโหนดยังเป็นของ `NavMeshAgent` ทั้งหมด
- ต้นไม้/เห็ด/ตอไม้/กระท่อมในซีนจริงยังไม่มี Collider (กระท่อม `Pre-Build_Cabin_001` มี MeshRenderer 47 ชิ้น Collider 0) — ตอนนี้กวางเดินทะลุพืชได้ตรงกับ Player ตั้งใจไว้แล้ว แต่ถ้าจะสลับ NavMeshSurface ไปเก็บ geometry แบบ Physics Colliders ต้องเติม Collider ให้กระท่อมก่อน ไม่งั้นกวางจะเดินทะลุกระท่อม
- มี Creature ที่ตั้งค่า AI ไว้จริงแค่ 1 สายพันธุ์ (Deer) และมี Quest/Dialogue จริงแค่ 1 เควส ผูกกับ `deer_01` เท่านั้น — พืช 5 ชนิด (Chan/FlyAgaric/Honey/Turkey/Puffball) ถ่ายได้ NPC มีบทพูดให้ความรู้ตอนถ่ายติดครบ แต่ไม่มีเควสผูก
- `Puffball` มี Profile/JournalEntry/PhotoSubject wiring ครบแล้ว แต่ยังไม่ได้ใส่เข้า `JournalManager.allEntries` ในซีน
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
