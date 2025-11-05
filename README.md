# AR(Augmented Reality) Slope – HoloLens 기반 증강현실 레이싱 게임
한밭대학교 컴퓨터공학과 **예용팀**  
> 현실 물체를 인식하여 트랙을 자동으로 생성하고, 실제 공간에서 주행 가능한 증강현실(AR) 레이싱 게임입니다.


## 팀 구성
- 20222044 정진용  
- 20221996 신예솔  
- 20221122 최예진  


## <u>Teamate Project Background</u>
### 필요성
- 최근 증강현실(AR) 기술은 엔터테인먼트뿐만 아니라 교육, 훈련, 산업 등 다양한 분야로 확장되고 있음.  
- 하지만 대부분의 AR 게임은 단순히 가상의 객체를 덧씌우는 수준에 머물러, **현실 공간의 지형·물체와의 상호작용이 부족함.**
### 기존 해결책의 문제점
- 기존 AR 게임은 현실 공간 인식 정확도가 낮아 **지형 반영형 콘텐츠 구현이 어려움.**  
- 대부분의 콘텐츠가 **고정된 트랙 또는 단순 평면 기반 게임**으로 현실감이 떨어짐.


## System Design
### System Requirements
- Microsoft **HoloLens 2**
- Unity 6, MRTK 2
- Photon PUN (로컬 멀티플레이)
- Convex Hull 기반 Mesh 생성 알고리즘
- Hand Tracking, Plane Detection, Spatial Mapping
### System Architecture
1. **공간 인식 단계**  
   - HoloLens의 Spatial Awareness System을 통해 현실 공간의 메쉬 데이터를 수집  
2. **물체 인식 및 트랙 생성**  
   - Convex Hull 알고리즘을 사용해 현실 물체의 윤곽선을 추출  
   - 인식된 형태를 바탕으로 3D 트랙 자동 생성  
3. **주행 및 상호작용**  
   - 차량 오브젝트가 인식된 트랙 위를 주행  
   - 손 인식(Hand Tracking)을 통한 직접 조작 및 상호작용  
4. **로컬 멀티플레이**  
   - 동일 공간 내 두 사용자가 실시간으로 대전 가능


## Case Study
### Description
- 사용자는 게임 시작 시 물체를 선택하여 트랙을 생성  
- 현실 공간의 배치에 따라 매번 다른 트랙이 형성되어 **유동적인 플레이 환경**을 제공  
- 멀티플레이 환경에서 상대방과 동시에 주행하며 **현실-가상 융합 체험** 가능  


## Conclusion
- 본 프로젝트는 현실 물체를 인식하고 이를 가상 트랙으로 변환함으로써, 현실 공간과 상호작용하는 새로운 형태의 증강현실 게임을 제시함.  
- 단순한 시각적 AR 콘텐츠에서 벗어나, **공간 분석·상호작용 중심의 AR 기술 가능성**을 확인함.  


## Project Outcome
- 2025년 한밭대학교 컴퓨터공학과 캡스톤 디자인 발표회 출품작
- 현실 공간 인식 기반의 AR 트랙 생성 및 실시간 상호작용 기술 연구 논문 작성 중  


## Tech Stack
| 구분 | 기술명 |
|------|---------|
| Engine | Unity 6 (C#) |
| SDK | MRTK2, OpenXR |
| Device | Microsoft HoloLens 2 |
| Network | Photon PUN |
| Algorithm | Convex Hull, Plane Detection |
| Input | Hand Tracking |


## License
이 프로젝트는 학술 목적의 연구 및 전시용으로만 사용됩니다.
