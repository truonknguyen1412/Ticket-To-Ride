\# Ticket-To-Ride Vietnam — Game Rules v1



\*\*Project:\*\* Ticket-To-Ride Vietnam  

\*\*Course:\*\* NT106  

\*\*Status:\*\* Draft — Stage 0  

\*\*Document version:\*\* 1.0



\## 1. Overview



Ticket-To-Ride Vietnam is a multiplayer, Vietnam-themed railway board game inspired by the original Ticket to Ride board game. Players collect train cards, claim railway routes, complete destination tickets, and compete for the highest final score.



The map is inspired by Vietnam's geography. Gameplay balance and route alternatives take priority over exact geographic distances.



\## 2. Players and Components



\- Each match supports 2–4 players.

\- Each player has 45 train pieces.

\- Each player has a distinct train color.

\- The train-card deck contains 110 cards:

&#x20; - 8 regular colors, with 12 cards per color.

&#x20; - 14 locomotive cards.

\- The board contains more than 40 locations and must include Quang Ngai.

\- The destination-ticket deck will contain approximately 60 tickets. The exact locations and ticket values remain to be designed.



\## 3. Setup



1\. Each player receives 4 train cards.

2\. Reveal 5 train cards face up.

3\. Each player draws 3 destination tickets and must keep at least 1.

4\. Determine the first player using a rule to be finalized.



\## 4. Turn Structure



On each turn, a player must perform exactly one action:



1\. Draw train cards.

2\. Claim a railway route.

3\. Draw destination tickets.



Players cannot perform multiple actions during the same turn.



\## 5. Drawing Train Cards



\- A player normally draws up to 2 train cards per turn.

\- If the first face-up card drawn is a locomotive, the player takes only that card for the turn.

\- If 3 or more locomotives appear among the 5 face-up cards, discard all 5 and reveal 5 replacements.

\- If the draw pile runs out and discarded train cards are available, shuffle the discards to create a new draw pile.

\- If no train cards remain in either pile, no further train cards can be drawn.



\## 6. Claiming Routes



\- Routes have lengths of 1–6 train pieces.

\- To claim a route, a player must submit a valid set of train cards matching the route's color and length, and have enough train pieces remaining.

\- Claimed-route points are awarded immediately.

\- A claimed route cannot be taken over by another player.

\- If a desired route is occupied, players must use an available alternative or detour.

\- Double routes follow these restrictions:

&#x20; - In 2–3 player matches, only one branch of a double route may be claimed.

&#x20; - In 4-player matches, both branches may be claimed.



\### Route Scoring



| Route length | Points |

|---:|---:|

| 1 | 1 |

| 2 | 2 |

| 3 | 4 |

| 4 | 7 |

| 5 | 10 |

| 6 | 15 |



\## 7. Drawing Destination Tickets



\- Draw 3 destination tickets and keep at least 1.

\- If exactly 3 tickets remain in the draw pile, shuffle discarded destination tickets back into the pile before drawing.

\- If neither the draw pile nor the discard pile contains tickets, the player cannot draw destination tickets and must choose another action.

\- The exact destination list and ticket values will be documented separately.



\## 8. End of the Game



When any player has 2 or fewer train pieces remaining after completing their turn, every player, including that player, receives one final turn.



After the final turns:



\- Completed destination tickets add their printed points.

\- Incomplete destination tickets subtract their printed points.

\- Route points already earned remain part of the score.

\- The player or players with the highest final score win.

\- The detailed tie-break sequence remains to be finalized.



\## 9. Longest Continuous Route



The player with the longest continuous route receives a 10-point bonus.



If multiple players tie for the longest continuous route, every tied player receives 10 points.



The exact algorithm for calculating route continuity, including branches and loops, will be specified in the technical design.



\## 10. Accounts, Reputation, and Ranked Play



\### Reputation



\- Every account starts with 100 reputation points.

\- Completing a match grants 5 reputation points.

\- Each confirmed violation deducts 10 reputation points.

\- Reputation from 60 to 100: casual and ranked play are allowed.

\- Reputation from 30 to 59: casual play only.

\- Reputation from 1 to 29: casual and ranked play are temporarily restricted according to a violation-based ban policy.

\- Reputation at 0: the account is banned until the recovery policy is satisfied.



The reputation cap, recovery process, exact ban durations, and violation definitions remain to be finalized.



\### Rank Points



\- Initial rank points: 100.

\- Match win: +50 rank points.

\- Match loss: -25 rank points.

\- Match draw: +20 rank points.



Rank tiers, point thresholds, and the effect of casual matches on rank remain to be finalized.



Reputation and rank points are separate systems.



\## 11. Multiplayer and Server Authority



\- The game uses a client-server architecture.

\- The client uses C# Windows Forms.

\- Clients communicate with a TCP server.

\- The server validates actions and owns the authoritative match state.

\- SQL Server stores account information, match history, and recoverable match state.

\- Invalid or illegal actions must be rejected by the server.



The detailed network message format and database schema will be documented separately.



\## 12. Disconnection and Recovery



\- A match should continue when a player disconnects.

\- When that player's turn arrives, the server waits for a configured timeout.

\- If the player does not reconnect before the timeout, they are marked as having left the match and the remaining players continue.

\- Reconnection is subject to the final reconnection policy.

\- After a server restart, the system restores saved match state and asks match participants to confirm continuation.

\- If all required participants confirm before the deadline, the match resumes.

\- If a participant fails to confirm, that participant is removed and the remaining players continue, subject to valid match rules.

\- The final persistence, recovery, and penalty details remain to be finalized.



\## 13. Open Decisions



The following items must be completed before the rules are finalized:



1\. Full map, route connections, route colors, and double-route layout.

2\. Destination-ticket list and point values.

3\. First-player selection rule.

4\. Tie-break sequence.

5\. Reputation cap, recovery rules, violation definitions, and ban durations.

6\. Rank tiers and point thresholds.

7\. Exact behavior when too few players remain after disconnections.

8\. TCP message protocol, database schema, and recovery implementation.



\## 14. Version History



\- \*\*v1.0:\*\* Initial Stage 0 draft based on the team's current decisions.

