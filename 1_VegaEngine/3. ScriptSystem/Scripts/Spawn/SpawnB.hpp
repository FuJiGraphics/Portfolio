#pragma once
#include <VegaEngine2.h>
#include "Utils/Timer.h"
#include "SpawnArea.hpp"

namespace fz {

	class SpawnBScript : public VegaScript
	{
	public:
		Timer timer;
		SpawnArea area;
		int SpawnLimitCount = 30;
		int CurrentSpawnCount = 0;

		void Start() override
		{
			timer["Spawn"].Start(10.f);
			const sf::Vector2f& pos = GetComponent<TransformComponent>().Transform.GetTranslate();
			const sf::Vector2f& halfSize = GetComponent<BoxCollider2DComponent>().GetHalfSize();
			area.SetPosition({ pos.x - halfSize.x, pos.y - halfSize.y });
			area.SetSize(GetComponent<BoxCollider2DComponent>().GetSize());
		}

		void OnDestroy() override
		{

		}

		void OnUpdate(float dt) override
		{
			timer.Update(dt);

			if (timer["Spawn"].Done())
			{
				timer["Spawn"].Start(10.f);
				Spawn();
			}
		}

		void Spawn()
		{
			if (CurrentSpawnCount > SpawnLimitCount)
				return;

			CurrentSpawnCount++;
			sf::Vector2f spawnPos = area.Spawn();
			int ran = Random(0, 2);
			if (ran == 0)
				GetCurrentScene()->Instantiate("Spoa", spawnPos);
			if (ran == 1)
				GetCurrentScene()->Instantiate("Snail", spawnPos);
			if (ran == 2)
				GetCurrentScene()->Instantiate("RibbonPig", spawnPos);
		}

	}; // class
} // namespace fz

