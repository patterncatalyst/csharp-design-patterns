"""One NSubstitute double, two jobs: stub return values (Arrange) and verify recorded calls (Assert)."""

from generate_diagram import emit

HEAD_Y, HEAD_H = 15, 85
PANEL_Y, PANEL_H = 145, 420        # the gap under the header holds the two arrow labels beside their arrows
LP, RP, PW = 20, 490, 450
BX, BW = 20, 390                   # box inset and width inside a panel
ROWS = [PANEL_Y + 35 + i * 80 for i in range(5)]
H, H_LAST = 65, 55

STUB = [
    ("accent", ["Substitute.For<ITaxPolicy>()", "creates the double; members start at their type default"]),
    ("box", [".ApplyTax(100m).Returns(110m)", "stubs the return for that exact call expression"]),
    ("box", ["GetBaseRateAsync(...).Returns(money)", "wraps a plain value in a completed Task<T>",
             "Arg.Any<CancellationToken>() stubs the token position"]),
    ("box", ["ApplyTax(100m)→110m, ApplyTax(200m)→220m", "two stubs on one substitute, matched by argument"]),
    ("ghost", ["Unstubbed call", "returns default(T): 0m, empty, or null"]),
]
VERIFY = [
    ("accent", ["Received(1)", "asserts the call happened once, with matching arguments"]),
    ("box", ["DidNotReceive()", "asserts a matching call never happened"]),
    ("box", ["Arg.Any<T>()", "matches any value; this position does not matter"]),
    ("box", ["Arg.Is<T>(predicate)", "matches only arguments where the predicate holds"]),
    ("sub", ["await carrier.Received(1)...", "verification is a Task too; missing await checks nothing"]),
]


def column(panel, items):
    return [{"x": panel + BX, "y": ROWS[i], "w": BW, "h": H_LAST if i == len(items) - 1 else H,
             "style": s, "lines": lines} for i, (s, lines) in enumerate(items)]


def chain(panel):
    cx = panel + BX + BW / 2
    return [{"x1": cx, "y1": ROWS[i] + H, "x2": cx, "y2": ROWS[i + 1]} for i in range(4)]


def entry(panel, label, lx):
    cx = panel + BX + BW / 2
    return {"x1": cx, "y1": HEAD_Y + HEAD_H, "x2": cx, "y2": PANEL_Y, "amber": True,
            "label": label, "lx": lx, "ly": 4}


emit(
    "10-nsubstitute-stub-vs-verify",
    960,
    585,
    bands=[
        {"x": LP, "y": PANEL_Y, "w": PW, "h": PANEL_H, "fill": "#fafafa",
         "label": "Stub (Arrange): make the double return values"},
        {"x": RP, "y": PANEL_Y, "w": PW, "h": PANEL_H, "fill": "#fafafa",
         "label": "Verify (Assert): check the double was called"},
    ],
    nodes=[
        {"x": 60, "y": HEAD_Y, "w": 840, "h": HEAD_H, "style": "ink",
         "lines": ["One substitute, two jobs",
                   "Arrange: stub supplies return values, the input the SUT consumes",
                   "Assert: verify reads recorded calls, the output the SUT produced",
                   "Arg.Any<T> and Arg.Is<T> apply on both sides"]},
        *column(LP, STUB),
        *column(RP, VERIFY),
    ],
    edges=[
        entry(LP, "feeds Act's inputs", 58),
        entry(RP, "reads calls after Act", 63),
        *chain(LP),
        *chain(RP),
    ],
)
