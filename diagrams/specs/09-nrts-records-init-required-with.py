"""record with: copy object A, override the named members, return a distinct object B."""

from generate_diagram import emit

PW, PH, PY = 400, 310, 58          # panel width, height, top
LP, RP = 40, 640                   # panel x; the gap holds the with { ... } labels clear of both panels
BX, BW = 20, 360                   # box inset and width inside a panel
CONS_Y, CONS_H = 95, 95
ROUTE_Y, ROUTE_H = 210, 65
OUT_Y, OUT_H = 295, 55
W = RP + PW + 40
MID = W // 2


def col(panel):
    return panel + BX


def stack_edges(panel):
    cx = col(panel) + BW // 2
    return [
        {"x1": cx, "y1": CONS_Y + CONS_H, "x2": cx, "y2": ROUTE_Y},
        {"x1": cx, "y1": ROUTE_Y + ROUTE_H, "x2": cx, "y2": OUT_Y},
    ]


CONSIGNMENT_LINES = ["required ConsignmentId = 4471", "required Weight = 1200 kg",
                     'init ReferenceNumber = "REF-4471"']
L_EDGE, R_EDGE = col(LP) + BW, col(RP)

emit(
    "09-nrts-records-init-required-with",
    W,
    470,
    bands=[
        {"x": LP, "y": PY, "w": PW, "h": PH, "fill": "#fafafa", "label": "original instance, unchanged"},
        {"x": RP, "y": PY, "w": PW, "h": PH, "fill": "#fff8ef", "label": "new instance returned by with"},
    ],
    nodes=[
        {"x": col(LP), "y": CONS_Y, "w": BW, "h": CONS_H, "style": "accent",
         "lines": ["Consignment  (object A)", *CONSIGNMENT_LINES]},
        {"x": col(LP), "y": ROUTE_Y, "w": BW, "h": ROUTE_H, "style": "sub",
         "lines": ["Route  (nested record)", "Chicago  →  Dallas"]},
        {"x": col(LP), "y": OUT_Y, "w": BW, "h": OUT_H, "style": "ink",
         "lines": ["object A is never mutated", "other holders of this reference still see Dallas"]},
        {"x": col(RP), "y": CONS_Y, "w": BW, "h": CONS_H, "style": "accent",
         "lines": ["Consignment  (object B)", *CONSIGNMENT_LINES]},
        {"x": col(RP), "y": ROUTE_Y, "w": BW, "h": ROUTE_H, "style": "box",
         "lines": ["Route  (nested record, replaced)", "Chicago  →  Houston"]},
        {"x": col(RP), "y": OUT_Y, "w": BW, "h": OUT_H, "style": "ink",
         "lines": ["object B is a distinct instance", "Destination corrected, every other member copied"]},
    ],
    edges=[
        *stack_edges(LP),
        *stack_edges(RP),
        {"x1": L_EDGE, "y1": 142, "x2": R_EDGE, "y2": 142, "amber": True,
         "label": "with { Route = ... }", "lx": 0, "ly": -10},
        {"x1": L_EDGE, "y1": 242, "x2": R_EDGE, "y2": 242, "amber": True,
         "label": 'with { Destination = "Houston" }', "lx": 0, "ly": -10},
    ],
    notes=[
        {"x": MID, "y": 28, "anchor": "middle", "bold": True, "size": 15, "color": "#888",
         "text": "record with: copy, change the named members, keep the original"},
        {"x": MID, "y": 46, "anchor": "middle", "size": 11, "color": "#555555",
         "text": "record = record class by default: reference-type storage, value-based equality and ToString"},
        {"x": MID, "y": 400, "anchor": "middle", "bold": True, "size": 12, "color": "#333333",
         "text": "with copies every member of object A, then overrides the named ones to produce object B."},
        {"x": MID, "y": 423, "anchor": "middle", "size": 11, "color": "#555555",
         "text": "Object A is untouched: nothing in the original is written to, and any other reference to it still sees Dallas."},
        {"x": MID, "y": 444, "anchor": "middle", "size": 11, "color": "#555555",
         "text": "obj1 == obj2 compares member values, not references, so two distinct instances with identical members are equal."},
    ],
)
