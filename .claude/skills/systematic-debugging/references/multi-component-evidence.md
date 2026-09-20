# Gathering evidence across component boundaries

Phase 1, step 4 of `systematic-debugging`, in full.

**When the system has several components** (CI → build → signing, API → service → database,
command → adapter → host), add diagnostic instrumentation **before** proposing any fix:

```text
For EACH component boundary:
  - Log what data enters the component
  - Log what data exits the component
  - Verify environment / config propagation
  - Check state at each layer

Run once to gather evidence showing WHERE it breaks
THEN analyze the evidence to identify the failing component
THEN investigate that specific component
```

## Example: a signing pipeline

```bash
# Layer 1: Workflow
echo "=== Secrets available in workflow: ==="
echo "IDENTITY: ${IDENTITY:+SET}${IDENTITY:-UNSET}"

# Layer 2: Build script
echo "=== Env vars in build script: ==="
env | grep IDENTITY || echo "IDENTITY not in environment"

# Layer 3: Signing script
echo "=== Keychain state: ==="
security list-keychains
security find-identity -v

# Layer 4: Actual signing
codesign --sign "$IDENTITY" --verbose=4 "$APP"
```

**This reveals** which layer fails: secrets → workflow ✓, workflow → build ✗.

## The same idea inside an add-in

A command calls an adapter, the adapter calls the host, the host returns elements. Log the input record
the use case received, the record the adapter built from the host, and the host's own read-back of what
was written. The first boundary where the values stop matching is where to look - not the line that
threw.
