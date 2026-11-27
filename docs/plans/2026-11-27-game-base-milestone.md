# Game base milestone (2026-11-27)

**Tag:** `milestone/2026-11-27-base`

This snapshot is the agreed “base state” for the main run: terrain relief, camera/command post framing, survey labels, and related slice work through 27 Nov 2026.

## Restore this version locally

```bash
git fetch origin tag milestone/2026-11-27-base   # if the tag was pushed
git checkout milestone/2026-11-27-base           # detached HEAD at the milestone
```

To keep working from here on a branch:

```bash
git checkout -b my-branch milestone/2026-11-27-base
```

To compare what changed since this milestone:

```bash
git diff milestone/2026-11-27-base...HEAD
```

## Push the tag (optional, for backup on GitHub)

```bash
git push origin milestone/2026-11-27-base
```

## Notes

- Planet mesh in the scene may differ until you **save the scene** after Play or run **Terraforming → Bake Planet 1 (Easy) Surface Into Open Scene**.
- Playtest artifacts under `Assets/Playtest/` are gitignored; harness output is not part of the tag.