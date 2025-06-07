# Meridian Chess Engine - Feature Roadmap

## Currently Implemented Features

### Search Algorithms
- Alpha-Beta with fail-hard cutoffs
- Iterative Deepening 
- Aspiration Windows (50cp windows, doubles on fail)
- Principal Variation Search (PVS)
- Quiescence Search
- Mate Distance Pruning
- Draw Detection (50-move, insufficient material)
- Illegal Position Handling

### Extensions (all 1 ply)
- Check Extension
- Singular Extension (depth 6+, margin 50cp)
- Recapture Extension  
- Passed Pawn Extension (7th rank)
- Null Move Threat Extension

### Reductions
- Late Move Reductions (LMR) - logarithmic formula with history integration
- Null Move Pruning (R=3)

### Pruning Techniques
- Futility Pruning (frontier nodes)
- SEE Pruning (bad captures)
- Late Move Pruning (depth 1-3)
- Razoring (depth ≤3, 300cp + 100*depth)
- Probcut (depth ≥5, 200cp margin)

### Move Ordering
- Transposition Table Move (1,000,000)
- Good Captures by SEE + MVV/LVA (900,000+)
- Promotions (800,000+)
- Killer Moves - 2 per ply + 2-ply ago (700,000/690,000/680,000)
- Counter Moves (670,000)
- Butterfly History [from][to]
- History Malus
- Bad Captures by SEE (-100,000+)

### Evaluation
- Material Values (P:100, N:320, B:330, R:500, Q:900)
- Piece Square Tables (separate mg/eg)
- Tapered Evaluation
- Pawn Structure: passed, isolated, doubled, backward, connected
- King Safety: shield, open files, attack units
- Mobility: per piece type, trapped pieces, outposts
- Endgame: king activity, passed pawn races, special endgames

### Infrastructure
- Single-threaded Transposition Table
- Thread-safe TT (cluster-based, lockless)
- Pawn Hash Table (16MB)
- Evaluation Cache (32MB)
- Static Exchange Evaluation (SEE)
- Multi-threading (Lazy SMP, 1-CPU threads)
- Internal Iterative Deepening (IID)

### UCI Protocol
- Full UCI compliance
- Pondering (go ponder/ponderhit)
- Options: Hash (1-16384MB), Threads, Ponder
- Time Management (basic)
- Debug commands (eval, display)

### Move Generation
- Magic Bitboards (rook/bishop)
- Pseudo-legal generation
- Capture-only generation
- Attack detection
- Move validation

## Future Enhancements

### High Priority
1. **History Pruning** - Prune moves with very poor history scores
2. **Multi-PV Analysis** - Show multiple best lines for analysis
3. **Advanced Time Management** - Move stability, time trouble handling
4. **Quiescence Futility Pruning** - Delta pruning in qsearch

### Medium Priority
5. **Contempt Factor** - Bias evaluation based on opponent strength
6. **Fail-High Reductions** - Reduce after multiple fail-highs
7. **Singular Extension at Root** - Special handling for root moves
8. **Opening Book Support** - Polyglot or custom format

### Low Priority
9. **Syzygy Tablebases** - Endgame tablebase probing
10. **Neural Network Evaluation** - NNUE or custom architecture
11. **Tuning Infrastructure** - Automated parameter optimization
12. **Extended Book Learning** - Learn from games

### Experimental
13. **Monte Carlo Tree Search** - For analysis mode
14. **Persistent Hash** - Save/load hash between games
15. **SMP Improvements** - ABDADA or other parallel search algorithms
16. **GPU Acceleration** - For evaluation or MCTS

## Performance Goals
- Maintain 1M+ NPS single-threaded
- Scale efficiently to 32+ threads
- Sub-100ms move time for opening/middlegame
- Accurate endgame play with tablebases
- ELO target: 3000+ CCRL

## Code Quality Goals
- Zero compiler warnings
- Comprehensive unit tests
- Performance benchmarks
- Documentation for all public APIs
- Cross-platform compatibility