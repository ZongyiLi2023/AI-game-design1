# AI For Game Design - Assignment 1

## Team 3 Members:
- **Li Zongyi**
- **Ding Yijie**
- **Ren Yu**
- **Deng Junkai**

## Description:

In this assignment, we are tasked with developing AI for tanks in a game environment. The AI implementation involves various states, such as patrolling, attacking, and fleeing, which allow the tanks to exhibit dynamic behavior in response to the player's actions and environmental factors. The primary focus is on creating intelligent decision-making and behavior transitions between states to improve gameplay engagement.

### Key Components:

- **Tank State Machine**: The tanks' AI is built using a finite state machine (FSM) architecture. Each tank has multiple states like `Patrolling`, `Attack`, `Flee`, and `StrongAttack`. The state transitions are determined based on proximity to the player, health status, and obstacles in the environment.

- **AttackState**: In this state, the tank actively targets the player, calculating the optimal launch force to hit the target based on distance and angle. The tanks also form triangular formations for coordinated attacks, adjusting their positions dynamically based on the number of tanks engaged.

- **StrongAttackState**: This state is similar to the `AttackState` but focuses on more aggressive, coordinated attacks. Tanks in this state form a triangular formation and continuously engage the target. They also detect obstacles and adjust their position or path accordingly, using raycasting for obstacle detection.

- **FleeState**: The tanks enter this state when their health drops below a certain threshold, causing them to flee from the player. The tanks rotate away from the player and navigate to a safe distance while avoiding obstacles along the way. If the tank successfully flees for a certain duration, it transitions into a stronger attack state.

- **Obstacle Avoidance**: Tanks detect obstacles in their path using raycasting and dynamically adjust their movement to avoid collisions. This is especially critical during the `FleeState` and `StrongAttackState`, where tanks must evade obstacles while fleeing or attacking.

### Objective:

The main goal of the assignment is to implement a robust AI system that enhances the tank's behavior, making them responsive to environmental challenges and the player's actions. By transitioning between various states and using obstacle avoidance techniques, the AI creates a more engaging and challenging experience for the player.

