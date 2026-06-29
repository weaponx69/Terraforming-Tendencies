import re, json

with open('Assets/Scripts/UI/Components/ColonyIntegrityBarGraph.asset', 'r') as f:
    content = f.read()

match = re.search(r"_json:\s*'(.+?)'\s*\n\s*_objectReferences:", content, re.DOTALL)
if match:
    json_str = match.group(1)
    try:
        data = json.loads(json_str)
        graph = data.get('graph', {})
        elements = graph.get('elements', [])
        print(f'Variables Kind: {graph.get("variables",{}).get("Kind","N/A")}')
        print(f'Elements count: {len(elements)}')
        vars_content = graph.get("variables",{}).get("collection",{}).get("$content",[])
        print(f'Variable names: {[v["name"] for v in vars_content]}')
        if elements:
            for el in elements[:5]:
                print(f'  - {el.get("$type","unknown")} at ({el.get("position",{}).get("x",0)},{el.get("position",{}).get("y",0)})')
        else:
            print('  (no elements - graph is empty!)')
    except json.JSONDecodeError as e:
        print(f'JSON PARSE ERROR: {e}')
else:
    print('Could not find _json field')
