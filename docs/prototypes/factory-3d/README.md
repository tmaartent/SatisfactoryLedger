# Prototype: a factory in 3D

The throwaway prototype behind the enhancement "A factory in 3D" ([possible enhancements](../../domain/possible-enhancements.md)). Not part of the app.

- `gather_area.py` takes one area of a snapshot (the map and analysis JSON from the API, and the game's Docs file) and writes it as one JSON file: buildings with their status and clearance boxes, belts and pipes with their curves.
- `template.html` draws that file with three.js (from a CDN): the JSON goes in place of `{{DATA}}`.

```bash
python gather_area.py docs/resources/en-US.json map.json analysis.json area.json
python -c "import sys; t=open('template.html').read(); open('factory.html','w').write(t.replace('{{DATA}}', open('area.json').read()))"
```

`map.json` is `GET /api/v1/world/snapshots/{id}/map`, `analysis.json` is `GET /api/v1/world/snapshots/{id}/analysis`. The area is set at the top of `gather_area.py`, in centimetres.
