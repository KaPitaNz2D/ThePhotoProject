# คู่มือแก้ไข Prefab ใน `TestSubject` (PhotoSubject + Collider)

เอกสารนี้สำหรับ Programmer ที่ต้องแก้ข้อมูลของวัตถุที่ "ถ่ายรูปได้" ใน Scene
`Assets/[04]Level/AsssetMaker/TestTerrainAndAsset/AssetAndModify.unity` (GameObject `TestSubject`)
แล้วต้องการให้การแก้ไขนั้นไปมีผลกับทุก Scene ที่ใช้ Prefab เดียวกัน

ข้อมูลในเอกสารตรวจจาก Scene ณ วันที่ 2026-10-10 — ถ้ามีคนแก้ Scene/Prefab ภายหลัง ให้ตรวจซ้ำก่อนเชื่อตาราง

---

## 1. ระบบถ่ายรูปอ่านข้อมูลจากไหน

`PhotoShooter` ([PhotoShooter.cs](../Assets/[02]Code/Script/Camera/Photograph/PhotoShooter.cs)) ยิง `SphereCastAll`
(radius = `castRadius`, ปัจจุบัน 0.5) แล้วกรองผลตามลำดับนี้ — **ตกข้อไหนวัตถุจะถูกข้ามเงียบๆ**

| # | เงื่อนไข | ตรวจที่ | ค่าปัจจุบัน |
|---|----------|---------|-------------|
| 1 | Collider อยู่ใน Layer ที่ `detectableLayer` เลือกไว้ | GameObject ที่มี Collider | `Default` เท่านั้น |
| 2 | Tag ของ GameObject ที่มี Collider = `photographableTag` | **GameObject ของ Collider เอง** (ไม่ใช่ Root) | `Photographable` |
| 3 | มี `PhotoSubject` บน GameObject เดียวกับ Collider หรือ Parent ข้างบน (`GetComponentInParent`) | Collider ขึ้นไปหา Root | – |
| 4 | `PhotoSubject.profile` ไม่ว่าง และ `CreatureId` ไม่ว่าง | [Photosavehandler.cs](../Assets/[02]Code/Script/Camera/Photograph/Photosavehandler.cs) | – |

ถ้าข้อ 1–3 ผ่านแต่ข้อ 4 ไม่ผ่าน จะเห็น Warning
`'<ชื่อ>' มี PhotoSubject แต่ยังไม่ได้ผูก CreatureProfile ไว้ — ข้ามไปก่อน` และภาพจะไม่ถูกนับเข้า Journal

`PhotoSubject.profile` ชี้ไปที่ `JournalSubjectProfile` (Asset `PlantProfile` / `CreatureProfile`)
ซึ่งเก็บ `creatureId` และ `journalEntry` — ค่า `creatureId` **ต้องตรงกับ JournalEntry แบบตัวพิมพ์เล็ก-ใหญ่เป๊ะ**

Profile ที่มีอยู่ (`Assets/[02]Code/Data/Profile/`):

| Asset | creatureId |
|-------|-----------|
| `PlantProfile/Chan.asset` | `Chan` |
| `PlantProfile/FlyAgaric.asset` | `FlyAgaric` |
| `PlantProfile/Honey.asset` | `Honey` |
| `PlantProfile/Puffball.asset` | `puffball` (ตัวพิมพ์เล็ก) |
| `PlantProfile/Turkey.asset` | `Turkey` |
| `CreatureProfile/Deer.asset` | `deer_01` |

---

## 2. วิธีแก้ให้ไปมีผลทุก Scene

**หลักการ:** ข้อมูลที่ต้องการให้ใช้ร่วมกัน (Collider, PhotoSubject, Tag, Profile) ต้องไปอยู่ที่ **Prefab Asset**
ไม่ใช่ค้างอยู่เป็น Override ของ Instance ใน Scene เดียว

### วิธี A — แก้ที่ Prefab Asset โดยตรง (แนะนำ)

1. ดับเบิลคลิก Prefab ใน Project (ดูชื่อไฟล์จากตารางข้อ 3) เพื่อเข้า Prefab Mode
2. แก้ Collider / PhotoSubject / Tag แล้วกด Save (Ctrl+S)
3. ทุก Scene ที่ใช้ Prefab นี้จะได้ค่าใหม่เอง **ยกเว้น** Instance ที่ Override ค่านั้นไว้
   (ใน Inspector จะเป็นตัวหนา + มีเส้นสีน้ำเงินด้านซ้าย → คลิกขวา **Revert** ถ้าต้องการให้ตามต้นแบบ)

### วิธี B — แก้ที่ Instance ใน Scene แล้ว Apply

1. แก้ค่าบน Instance ใน Hierarchy
2. คลิกขวาที่ Property / Component → **Apply to Prefab '<ชื่อ>'** หรือใช้เมนู **Overrides ▸ Apply All**
3. ห้าม Apply: `Position`, `Rotation`, `Scale` ของ Root (เป็นค่าเฉพาะตำแหน่งใน Scene นี้)
   และ `m_IsActive` ที่ขึ้นเป็น Override บน Root ของหลาย Prefab (ไม่มีผลอะไร ควร Revert ทิ้ง)

### ระวังเรื่อง Nested Prefab / Variant

หลาย Prefab ใน `TestSubject` ซ้อนกันเป็นชั้น เช่น `PF_TreeStump001WithChan001` (Variant) →
ภายในมี `PF_TreeStumpAlone_001` (Base) อีกชั้น ตอน Apply Unity จะถามปลายทาง:

- Apply ไป **Variant** (เช่น `PF_TreeStump001WithChan001`) → กระทบเฉพาะ Variant นั้น
- Apply ไป **Base** (เช่น `PF_TreeStumpAlone_001`) → กระทบ **ทุก Variant ที่ใช้ Base นี้**

แก้ Collider ของตอไม้ให้ทุกตัวเหมือนกัน → Apply ไป Base. แก้เฉพาะตัวเดียว → Apply ไป Variant

---

## 3. รายการ Prefab ใน `TestSubject`

ชื่อ Instance ใน Hierarchy **ไม่ได้บอกว่าเป็น Asset ไหนเสมอไป** ให้ดูคอลัมน์ Asset เป็นหลัก
(มีชื่อสลับกันอยู่ ดูข้อ 4.5)

พาธ Asset ย่อจาก `Assets/[03]Prefabs/`

| Instance ใน Scene | Prefab Asset | Collider (บน Prefab) | Tag ของ Collider | PhotoSubject / Profile |
|---|---|---|---|---|
| `PF_Chanterrella_001 (1)` | `Fungus/Chanterelle_Prefab/PF_Chanterrella_001` | Capsule, Trigger | Photographable | Root → `Chan` |
| `PF_Chanterrella_002 (1)` | `Fungus/Chanterelle_Prefab/PF_Chanterrella_002` | Capsule, Trigger | Photographable | Root → `Chan` |
| `PF_TreeStump001WithChan001 (1)` | `Fungus/Chanterelle_Prefab/PF_TreeStump001WithChan001` (Variant ของ `Woods/Stump/PF_TreeStumpAlone_001`) | ตอไม้: Capsule ไม่ Trigger · ลูก `Collider`: Capsule Trigger | ตอ: Untagged · `Collider`: Photographable | อยู่ที่ลูก `Collider` → `Chan` |
| `PF_TreeStump002WithChan001 (1)` | `Fungus/Chanterelle_Prefab/PF_TreeStump002WithChan001` (Variant ของ `PF_TreeStumpAlone_002`) | เหมือนข้างบน | เหมือนข้างบน | ลูก `Collider` → `Chan` |
| `PF_TreeStump001WithChan002 (1)` ⚠ | `Fungus/Chanterelle_Prefab/PF_TreeStump002WithChan002` | ตอไม้ + `PF_Chanterrella_001/002` ซ้อนอยู่ข้างใน | ตอ: Untagged · เห็ด: Photographable | ที่เห็ดแต่ละอัน → `Chan` |
| `PF_TreeStump002WithChan002 (1)` ⚠ | `Fungus/Chanterelle_Prefab/PF_TreeStump001WithChan002` | ตอไม้ + `PF_Chanterrella_001` ซ้อนอยู่ข้างใน | ตอ: Untagged · เห็ด: Photographable | ที่เห็ด → `Chan` |
| `PF_TreeStump001WithTurkey (1)` | `Fungus/TurkeyTail_Prefab/PF_TreeStump001WithTurkey` | Root: Capsule ไม่ Trigger · ลูก `TurkeyTail_003 (1..8)` ×8: Capsule ไม่ Trigger | Root: Photographable · ลูก: **Untagged** | ทั้ง Root และลูกทุกตัวมี PhotoSubject → `Turkey` |
| `HoneyMushroom_001 (1)` | `Fungus/HoneyMushroom/PF_HoneyMushroom_001` | Capsule ไม่ Trigger (height 0.01) | Photographable | Root → **ไม่มี Profile** |
| `HoneyMushroom_002 (1)` | `Fungus/HoneyMushroom/HoneyMushroom_002` | Capsule, Trigger | **Untagged** | Root → **ไม่มี Profile** |
| `PF_TreeStump001_WithHoney002` | `Fungus/HoneyMushroom/PF_TreeStump001WithHoney002` (Variant) | ตอไม้ Capsule + `HoneyMushroom_002` ×1 ซ้อนอยู่ | ตอ: Photographable (แต่ไม่มี PhotoSubject) · เห็ด: **Untagged** | เห็ด → **ไม่มี Profile** |
| `PF_TreeStump002WIthHoney001` | `Fungus/HoneyMushroom/PF_TreeStump002WIthHoney001` (Variant) | ตอไม้ Capsule + `HoneyMushroom_002` ×3 ซ้อนอยู่ | เหมือนข้างบน | เห็ด → **ไม่มี Profile** |
| `PF_FlyAgaric_003 (1)` … `_006 (1)` | `Fungus/FlyAgaric_Prefab/PF_FlyAgaric_003` … `_006` | Capsule, Trigger | Photographable | Root → `FlyAgaric` |
| `PuffBall_003 (2)` … `_007 (2)` | `Fungus/PuffBall_Prefab/PuffBall_003` … `_007` (Variant) | Sphere ไม่ Trigger | Photographable (ยกเว้น `_007` = **Untagged**) | Root → `puffball` |
| `JoePyed/JoePyeWeed_Col` | `Flora/JoePyeWeed_Col` | Box, Trigger | **Untagged** | Root → **ไม่มี Profile** |

ไม่มี Collider/PhotoSubject (เป็นภาพประกอบอย่างเดียว ไม่ต้องแก้ตามเอกสารนี้):
`Flora/PF_JoePyeWeed_01..03`, `PF_JoePyeWeed_Patch_01..03`, `PF_Goldenrod_*`

---

## 4. จุดที่ควรแก้ (พบตอนตรวจ Scene)

### 4.1 Profile ว่าง → ถ่ายติดแต่ไม่นับเข้า Journal
`PF_HoneyMushroom_001`, `HoneyMushroom_002`, `JoePyeWeed_Col` — ลาก `PlantProfile/Honey.asset`
ใส่ช่อง `Profile` ของ `PhotoSubject` ที่ Prefab (ส่วน `JoePyeWeed_Col` ยังไม่มี Profile ของต้นนี้ใน Project ต้องสร้าง `PlantProfile` ใหม่ก่อน)

### 4.2 Tag เป็น Untagged → ตรวจไม่เจอเลย
Collider ที่ Tag ไม่ใช่ `Photographable` จะถูกข้ามที่ข้อ 2 ทันที ต่อให้มี PhotoSubject ก็ตาม
ที่ต้องแก้: `HoneyMushroom_002` (ทำให้ตอที่ใส่เห็ดน้ำผึ้งทั้ง 2 แบบถ่ายไม่ติดด้วย), `PuffBall_007`, `JoePyeWeed_Col`
ที่ `TurkeyTail_003 (1..8)` เป็น Untagged เหมือนกัน แต่ Collider ของ Root ที่ Tag ถูกอยู่แล้วครอบทั้งกลุ่ม จึงถ่ายติดอยู่

### 4.3 Override ที่ยังไม่ Apply (ค่าอยู่แค่ใน Scene นี้)
| Instance | Property ที่ Override |
|---|---|
| `PF_FlyAgaric_003 (1)` | CapsuleCollider `height` = 0.34, `center.y` = 0.095 |
| `PF_FlyAgaric_004 (1)` | CapsuleCollider `height` = 0.29, `center.y` = 0.070 |
| `JoePyeWeed_Col` | BoxCollider `size` = (1, 1.66, 5.53), `center` = (0, 0.33, −2.27) |

ถ้าค่าเหล่านี้ถูกต้องแล้ว ให้ Apply เข้า Prefab; ถ้าไม่ใช่ให้ Revert

### 4.4 Collider ไม่ Trigger ปนกับ Trigger
Turkey, PuffBall, `HoneyMushroom_001` และตอไม้เป็น Collider ปกติ (ชนจริง) ส่วน Chanterelle, FlyAgaric, `HoneyMushroom_002`
เป็น Trigger — การตรวจของกล้องไม่ได้เลือกชนิด แต่ผู้เล่น/สัตว์จะเดินทะลุหรือติดต่างกัน
(การที่ SphereCast เจอ Trigger ขึ้นกับ Project Setting `Physics > Queries Hit Triggers` ซึ่งตอนนี้เป็นค่าเริ่มต้น = เปิด)
ควรตกลงกันว่าชนิดไหนควรเป็นแบบไหน แล้วแก้ที่ Prefab ให้สม่ำเสมอ

### 4.5 ชื่อ Instance ไม่ตรงกับ Asset
- Instance `PF_TreeStump001WithChan002 (1)` คือ Asset **`PF_TreeStump002WithChan002`**
- Instance `PF_TreeStump002WithChan002 (1)` คือ Asset **`PF_TreeStump001WithChan002`**

ถ้าไปแก้ตามชื่อใน Hierarchy จะได้ Prefab ผิดตัว → ให้คลิกขวา Instance แล้วเลือก
**Prefab ▸ Select Asset** (หรือกดลูกศรสีน้ำเงินข้างชื่อ) เพื่อเปิดไฟล์จริงเสมอ

### 4.6 `HoneyMushroom_001` Collider แบน
Capsule `height` = 0.01 น้อยกว่า `radius` = 0.19 ทำให้ Unity บีบเป็นทรงกลมรัศมี 0.19 ตรวจว่าตั้งใจหรือไม่

---

## 5. Checklist เมื่อเพิ่ม/แก้ Subject ใหม่

1. Prefab มี `Collider` อย่างน้อย 1 ตัวที่ **GameObject ของ Collider ตั้ง Tag = `Photographable`** และ Layer = `Default`
2. มี `PhotoSubject` อยู่ที่ GameObject เดียวกับ Collider หรือ Parent ของมัน (ถ้ามีหลาย Collider ใต้ Root เดียว วาง PhotoSubject ที่ Root ครั้งเดียวพอ — ดูตัวอย่าง `PF_TreeStump001WithChan001` ที่วางไว้ที่ลูก `Collider`)
3. ช่อง `Profile` ของ `PhotoSubject` ผูก Asset แล้ว และ `creatureId` ตรงกับ JournalEntry
4. ตั้งขนาด Collider ให้พอดีวัตถุ (ใช้ `ColliderGizmo` ช่วยดู — ข้อ 6)
5. Apply/Save ที่ Prefab แล้วทดสอบใน Scene ปลายทาง: เข้าโหมดถ่ายรูป เล็งแล้วจุดกลางจอต้องเปลี่ยนเป็นสี `detectedColor`

---

## 6. ตัวช่วยมองเห็น Collider ใน Scene View

เพิ่ม Component `ColliderGizmo` ([ColliderGizmo.cs](../Assets/[02]Code/Script/Debug/ColliderGizmo.cs))
ลงบน GameObject ที่มี `BoxCollider` หรือ `SphereCollider` จะเห็นกล่องสีเหลืองโปร่งใน Scene View
เฉพาะ Edit Mode (ไม่แสดงตอน Play และไม่เข้า Build) ตอนนี้ติดอยู่ที่ Prefab `Flora/Goldenrod_Col`
รองรับเฉพาะ Box และ Sphere — **ยังไม่วาด `CapsuleCollider`** ซึ่งเป็นชนิดที่ Prefab ส่วนใหญ่ใน `TestSubject` ใช้
(ถ้าต้องการ ให้เพิ่มกรณี Capsule ใน Script นั้น)
