"""Gathers one stacked area of the owner's world for the 3D prototype: buildings with their status and their clearance
boxes (local corners, from the Docs file), and belt/pipe curves. Units: centimetres, game axes (x east, y south, z up)."""
import json, re, sys

docs, map_path, analysis_path, out = sys.argv[1:5]
X0, X1, Y0, Y1 = 310_000, 326_000, -193_500, -184_500  # the area, in cm

data = json.loads(open(docs, "rb").read().decode("utf-16"))
num = r"(-?[\d.]+)"


def xyz(tag, s):
    m = re.search(tag + r"=\(X=" + num + ",Y=" + num + ",Z=" + num, s)
    return tuple(map(float, m.groups())) if m else None


def rotate(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    cx, cy, cz = y * vz - z * vy, z * vx - x * vz, x * vy - y * vx
    cx2, cy2, cz2 = y * cz - z * cy, z * cx - x * cz, x * cy - y * cx
    return (vx + 2 * w * cx + 2 * cx2, vy + 2 * w * cy + 2 * cy2, vz + 2 * w * cz + 2 * cz2)


def boxes_of(clearance):
    parts, depth, start = [], 0, None
    for i, ch in enumerate(clearance[1:-1], 1):
        if ch == "(":
            if depth == 0:
                start = i
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                parts.append(clearance[start:i + 1])
    boxes = []
    for p in parts:
        if "CT_Soft" in p or "IsValid=True" not in p:
            continue
        mn, mx = xyz("Min", p), xyz("Max", p)
        t = xyz("Translation", p) or (0, 0, 0)
        m = re.search(r"Rotation=\(X=" + num + ",Y=" + num + ",Z=" + num + ",W=" + num, p)
        q = tuple(map(float, m.groups())) if m else (0, 0, 0, 1)
        # Corner k: bit 2 = x max, bit 1 = y max, bit 0 = z max.
        corners = [rotate(q, (a, b, c)) for a in (mn[0], mx[0]) for b in (mn[1], mx[1]) for c in (mn[2], mx[2])]
        boxes.append([[round(x + t[0]), round(y + t[1]), round(z + t[2])] for x, y, z in corners])
    return boxes


shapes = {}
for nc in data:
    for c in nc.get("Classes", []):
        cd = c.get("mClearanceData")
        if isinstance(cd, str) and "IsValid=True" in cd:
            boxes = boxes_of(cd)
            if boxes:
                shapes[c["ClassName"]] = boxes
        if c.get("mDisplayName") and c.get("ClassName", "").startswith("Build_"):
            shapes.setdefault("__names__", {})[c["ClassName"]] = c["mDisplayName"]

world = json.load(open(map_path))
analysis = json.load(open(analysis_path))
status = {m["id"]: (m["limit"], m["utilisation"], m.get("limitingItem")) for m in analysis["machines"]}
status.update({g["id"]: (g["limit"], g["utilisation"], g.get("limitingItem")) for g in analysis["generators"]})


def inside(p):
    return X0 <= p["x"] <= X1 and Y0 <= p["y"] <= Y1


buildings = []
for kind in ("machines", "providers", "logistics", "consumers"):
    for b in world[kind]:
        if not inside(b["position"]):
            continue
        p = b["position"]
        s = status.get(b["id"])
        buildings.append({
            "kind": kind, "cls": b["className"], "recipe": b.get("recipe"),
            "p": [round(p["x"]), round(p["y"]), round(p["z"])], "yaw": b.get("yaw", 0),
            "status": s[0] if s else None, "util": round(s[1], 3) if s else None, "item": s[2] if s else None,
        })

conveyors = []
for c in world["conveyors"]:
    if any(inside(pt["location"]) for pt in c["curve"]):
        conveyors.append({"pipe": c["kind"] == "pipe", "curve": [
            [[round(pt["location"][a]) for a in "xyz"], [round(pt["arriveTangent"][a]) for a in "xyz"], [round(pt["leaveTangent"][a]) for a in "xyz"]]
            for pt in c["curve"]]})

used = {b["cls"] for b in buildings}
result = {
    "area": [X0, X1, Y0, Y1],
    "shapes": {k: v for k, v in shapes.items() if k in used},
    "names": {k: v for k, v in shapes["__names__"].items() if k in used},
    "buildings": buildings,
    "conveyors": conveyors,
}
open(out, "w").write(json.dumps(result, separators=(",", ":")))
zs = [b["p"][2] for b in buildings]
print(len(buildings), "buildings,", len(conveyors), "belts and pipes,", len(result["shapes"]), "shapes;",
      "z", min(zs) / 100, "to", max(zs) / 100, "m;", round(len(json.dumps(result)) / 1024), "KB")
print({k: sum(1 for b in buildings if b["kind"] == k) for k in ("machines", "providers", "logistics", "consumers")},
      "no shape:", sorted({b["cls"] for b in buildings if b["cls"] not in result["shapes"]}))
