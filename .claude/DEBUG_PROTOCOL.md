# Debugging & Testing Protocol

## Purpose

Establish clear communication patterns to avoid faulty assumptions during debugging sessions.

**Foundation**: This protocol applies the **Scientific Method** to debugging:
1. **Observe** - What's the current state?
2. **Question** - What changed?
3. **Hypothesize** - Form theory based on changes
4. **Test** - Isolate variables, test one at a time
5. **Analyze** - Did it work? Why or why not?
6. **Conclude** - Document what caused the issue

---

## Core Principles

### 0. **What Changed? (The Prime Directive)**
**When something breaks, ALWAYS ask first:**
- "What changed between working and broken?"
- "What did we just modify?"
- "What's different from the last working state?"

This is the **most powerful debugging question** and should be asked BEFORE diving into solutions.

**Example from this session:**
- GUI worked initially
- After adding config file, GUI stopped opening
- **Two changes**: config file + .resc script
- **Should have asked**: "We changed TWO things - let's isolate which one broke it"
- **Actual cause**: Config file with wrong syntax



### 1. **User Observations Are Ground Truth**
- If the user says "the GUI opened", IT OPENED
- If the user says "I see X", THEY SEE X
- Don't override user observations with tool output

### 2. **Absence of Evidence ≠ Evidence of Absence**
- No terminal output ≠ failure
- Background process ≠ not running
- No error message ≠ success

### 3. **Verify Before Pivoting**
- Confirm current state before troubleshooting
- Ask explicit questions about what user sees
- Don't assume failure from indirect signals

---

## Testing Protocol Checklist

When testing something with the user (GUI, web app, service, etc.):

### ✅ **Before Making Changes**
- [ ] Note current working state (baseline)
- [ ] Make ONE change at a time when possible
- [ ] Document what you're changing and why

### ✅ **Before Running Test**
- [ ] Explain what SHOULD happen
- [ ] Set clear success criteria
- [ ] Identify what output to expect WHERE (terminal vs GUI vs file)

### ✅ **After Running Test**
- [ ] **ASK**: "What do you see?" (before assuming anything)
- [ ] **ASK**: "Did [expected thing] happen?"
- [ ] **WAIT** for user response before proceeding
- [ ] Document what user reports as confirmed state

### ✅ **Before Troubleshooting**
- [ ] **ASK: "What changed?"** (files, config, commands, environment)
- [ ] **ASK: "When did it last work?"** (establish baseline)
- [ ] **ASK: "What's different now?"** (compare working vs broken state)
- [ ] List all changes made since last working state
- [ ] Verify current state with user
- [ ] Confirm what IS working
- [ ] Confirm what ISN'T working
- [ ] Check assumptions explicitly

### ✅ **During Debugging**
- [ ] State your assumptions out loud: "I'm assuming X because Y"
- [ ] Ask for confirmation: "Can you verify X?"
- [ ] Use `AskUserQuestion` tool for critical checkpoints
- [ ] Don't chain multiple "fixes" without verification

---

## Common Anti-Patterns to Avoid

### ❌ **Ignoring Recent Changes**
```
Bad:  "It's broken, let me investigate all possible causes"
Good: "What changed? Let's check the config file we just added"
```

### ❌ **Multiple Simultaneous Changes**
```
Bad:  Change config file AND script AND command all at once
Good: Change one thing, test, then change next thing
```

### ❌ **Not Establishing Baseline**
```
Bad:  "Try this fix, try that fix, try another..."
Good: "Let's revert to last working state first, then add changes back methodically"
```

### ❌ **Silent Assumptions**
```
Bad:  "No output in terminal, must be broken, let me fix it"
Good: "I don't see terminal output. Did a window open on your screen?"
```

### ❌ **Ignoring User Reports**
```
Bad:  User: "GUI opened" → Assistant: "Let me check why GUI won't open..."
Good: User: "GUI opened" → Assistant: "Great! What do you see in it?"
```

### ❌ **Assumption Cascades**
```
Bad:  Assume failure → Try fix A → Assume still broken → Try fix B → ...
Good: Verify state → Identify problem → Apply fix → Verify fix worked
```

### ❌ **Tool Output > User Input**
```
Bad:  "My grep shows nothing, so the feature doesn't exist"
Good: "My grep shows nothing. Did you see it working? Maybe it's in a different file"
```

---

## Verification Question Templates

Use these when uncertain:

### For GUI Applications
```
"Did a window open on your screen?"
"What do you see in the [window/terminal/browser]?"
"Is the text readable or too small?"
```

### For Command-Line Tools
```
"What output do you see in your terminal?"
"Did the command complete or is it still running?"
"Any error messages?"
```

### For Background Processes
```
"Can you check if [process name] is running?"
"What's in [log file / output file]?"
"Do you see any indication it started?"
```

### Before Major Changes
```
"Before I change X, can you confirm Y is currently [working/not working]?"
"Let's verify: does [feature] work now, or is it still broken?"
```

---

## When to Use AskUserQuestion Tool

Use the `AskUserQuestion` tool (not just text) when:

1. **Critical fork in the road**: Multiple valid approaches, need user preference
2. **Verification checkpoint**: Need explicit confirmation before proceeding
3. **Unclear state**: Can't determine if something worked from available data
4. **User preference needed**: Multiple valid solutions with different trade-offs

Example:
```typescript
AskUserQuestion({
  questions: [{
    question: "Did the GUI window open with the test running?",
    header: "GUI Status",
    options: [
      {label: "Yes, I see it", description: "Window opened successfully"},
      {label: "No window appeared", description: "GUI didn't start"},
      {label: "Window opened briefly then closed", description: "Started but crashed"}
    ]
  }]
})
```

---

## The Scientific Method for Debugging

### Control Your Variables

Like any scientific experiment, debugging requires **isolating variables**:

```
Scientific Experiment:        Debugging:
- Control group              → Last working state (baseline)
- Test group                 → Current broken state
- Independent variable       → The ONE thing you change
- Dependent variable         → Does it work? (success/failure)
- Confounding variables      → Multiple changes at once (AVOID!)
```

**Golden Rule**: Change ONE thing at a time, measure result, repeat.

---

## Differential Diagnosis: The "What Changed?" Framework

When something stops working, use this systematic approach:

### 1. **Establish Timeline**
```
What was the last working state?
What changes happened since then?
When exactly did it break?
```

### 2. **Enumerate All Changes**
List EVERYTHING that changed (even "small" things):
- Files modified
- Configuration changes
- Commands run
- Environment variables
- System updates
- User actions

### 3. **Isolate Variables** (Control the Experiment!)
If multiple things changed:
```
Bad:  Try to fix everything at once (confounding variables)
Good: Revert all changes, establish control, then add back ONE change at a time

Example:
- Baseline (control): GUI works, no config, old script
- Test 1: Add config only → GUI breaks? Config is the problem!
- Test 2: Revert, fix script only → GUI breaks? Script is the problem!
- Test 3: Both changes → If breaks, you learned nothing (confounded)
```

This is **literally** the scientific method.

### 4. **Binary Search**
If many changes, use binary search:
```
- Revert half the changes
- Does it work now?
  - Yes → Problem was in reverted half
  - No → Problem is in remaining half
- Repeat until isolated
```

### 5. **Compare States**
```
Working State:           Broken State:
- Config file: absent    - Config file: present (wrong syntax)
- Script: old syntax     - Script: fixed syntax
                         → TWO changes! Which broke it?
```

---

## Debugging Session Template

### 1. **State Assessment**
- What are we testing?
- What should happen?
- What does the user currently see?
- **What changed since it last worked?** ← CRITICAL

### 2. **Hypothesis Formation**
- **First**: Consider what changed (most likely cause)
- State assumption explicitly: "I think X is failing because Y"
- Ask: "Does that match what you're seeing?"
- **Validate**: "Let me check if reverting [change] fixes it"

### 3. **Verification**
- Run test/check
- Ask: "What changed? What do you see now?"
- Confirm: "Did [expected result] happen?"

### 4. **Iteration or Conclusion**
- If fixed: "Confirm it's working as expected?"
- If not fixed: "Let's verify [next thing] before trying [next fix]"

---

## Project-Specific Notes

### For Renode Testing
- GUI may open on different workspace/display
- Headless mode (`--disable-xwt`) is more reliable for automated testing
- Analyzer windows are separate from monitor console
- Font config changes require Renode restart
- Background builds won't show GUI output in launching terminal

### For Firmware Testing
- Logging may be compiled out (`DEBUG_LOGGING_ENABLED`)
- Semihosting output appears in analyzer window, not terminal
- Firmware may boot but get stuck before reaching main()
- Check `strings firmware.elf` to see if log messages exist

---

## Meta-Protocol: Improving This Protocol

When we encounter a new debugging failure mode:

1. Document what went wrong
2. Identify the assumption/communication failure
3. Add pattern to this document
4. Share the learning

This is a living document. Update it when we learn new patterns.

---

---

## Real Example: The Config File Case Study

**What happened:**
1. GUI opened successfully (baseline working state)
2. Added config file with font settings
3. Fixed .resc script syntax
4. GUI stopped opening

**What I did wrong:**
- Didn't ask "What changed?"
- Didn't isolate the two changes
- Assumed problem was complex (Wayland, display issues, etc.)

**What I should have done:**
1. Ask: "We changed TWO things - config file and script. Let's test which broke it"
2. Remove config file, test → GUI opens!
3. Conclusion: Config file is the problem
4. Fix config syntax (took 2 minutes once identified)

**Lesson:** When something breaks after changes, the change IS the problem until proven otherwise.

**Scientific Method Applied:**
- ❌ Violated: Changed multiple variables simultaneously (config + script)
- ❌ Violated: Didn't establish control group (baseline working state)
- ❌ Violated: Didn't isolate variables (test changes independently)
- ✅ Should have: Reverted to baseline, tested one change at a time

This isn't just good debugging - it's **basic science**.

---

**Last Updated**: 2026-02-13
**Context**: Learned from GUI testing confusion during Renode semihosting implementation
**Key Learning**: "What changed?" is the most powerful debugging question
