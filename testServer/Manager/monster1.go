// manager/monster.go
package manager

import (
	pb "testServer/Messages"
	"testServer/behavior"
	"testServer/common"
	"time"
)

type Monster struct {
	ID        int
	X, Z      float32
	Health    int
	MaxHealth int
	Target    *common.Point
	Path      []common.Point
	PathIndex int
	AI        behavior.Node
}

func NewMonster(id int, x, y float32, maxHealth int, path []common.Point) *Monster {
	m := &Monster{
		ID:        id,
		X:         x,
		Z:         y,
		Health:    maxHealth,
		MaxHealth: maxHealth,
		Path:      path,
		PathIndex: 0,
	}
	m.AI = CreateMonsterBehaviorTree(m)
	return m
}
func (m *Monster) GetPosition() common.Point {
	return common.Point{X: m.X, Z: m.Z}
}

func (m *Monster) SetPosition(x, y float32) {
	m.X = x
	m.Z = y
}

func (m *Monster) SetTarget(target *common.Point) {
	m.Target = target
}

func (m *Monster) GetTarget() *common.Point {
	return m.Target
}

func (m *Monster) GetPath() []common.Point {
	return m.Path
}

func (m *Monster) GetPathIndex() int {
	return m.PathIndex
}

func (m *Monster) SetPathIndex(idx int) {
	m.PathIndex = idx
}

func (m *Monster) GetID() int {
	return m.ID
}

func (m *Monster) GetHealth() int {
	return m.Health
}

func (m *Monster) SetHealth(health int) {
	m.Health = health
	if m.Health < 0 {
		m.Health = 0
	}
	if m.Health > m.MaxHealth {
		m.Health = m.MaxHealth
	}
}

func (m *Monster) GetMaxHealth() int {
	return m.MaxHealth
}

func (m *Monster) IsDead() bool {
	return m.Health <= 0
}

func (m *Monster) Update() {
	if !m.IsDead() {
		m.AI.Execute()
	}
}

func (mm *MonsterManager) GetMonster(id int32) (*Monster, bool) {
	monster, exists := mm.monsters[id]
	return monster, exists
}

func (m *Monster) TakeDamage(damage int) {
	m.Health -= damage
	if m.Health < 0 {
		m.Health = 0
	}

	damageMsg := &pb.GameMessage{
		Message: &pb.GameMessage_MonsterDamage{
			MonsterDamage: &pb.MonsterDamage{
				MonsterId: int32(m.ID),
				Damage:    float32(damage),
				CurrentHp: int32(m.Health),
			},
		},
	}

	GetPlayerManager().Broadcast(damageMsg)
}

func CreateMonsterBehaviorTree(monster *Monster) behavior.Node {
	playerManager := GetPlayerManager()
	netManager := GetNetManager()
	state := &behavior.AttackState{}

	return behavior.NewSelector(
		behavior.NewSequence(
			behavior.NewDetectPlayer(monster, 50.0, playerManager, netManager),
			behavior.NewSelector(
				behavior.NewSequence(
					behavior.NewDetectPlayer(monster, 2.0, playerManager, netManager),
					behavior.NewMeleeAttack(monster, 2.0, 10, 4*time.Second, playerManager, netManager, state),
					behavior.NewWait(1*time.Second, false, state),
				),
				behavior.NewMutuallyExclusiveSelector(0.33,
					[]behavior.Node{
						behavior.NewSequence(
							behavior.NewRangedAttack(monster, 50.0, 8, 10*time.Second, playerManager, netManager, state),
							behavior.NewWait(1*time.Second, false, state),
						),
						behavior.NewSequence(
							behavior.NewMeteorAttack(monster, 59.0, 15, 10*time.Second, playerManager, netManager, state),
							behavior.NewWait(2*time.Second, false, state),
						),
					},
				),
				behavior.NewChase(monster, 50.0, playerManager, netManager),
			),
		),
	)
}
