/**
 * Grading a short answer: numbers and expressions.
 *
 * An answer is parsed into an expression and compared with the key by
 * value. Without variables that is one number each, equal to within a part
 * in 10^9. With variables, both sides are evaluated at the same eight
 * points, and every point must agree. "1/2", "0.5", and "2/4" are the same
 * answer. "0.333" is not 1/3.
 *
 * The grammar: numbers (12, 1.5, .5), + - * / ^ and parentheses, implicit
 * multiplication (2x, 3(4), 2sqrt(3)), the functions sqrt, abs, sin, cos,
 * tan, ln, log (base 10), and exp, the constant pi (or π), and single-letter
 * variables. A leading "x =" is dropped. Anything else is not an answer.
 */

export class ParseError extends Error {}

type Node =
  | { k: "num"; v: number }
  | { k: "var"; name: string }
  | { k: "neg"; a: Node }
  | { k: "bin"; op: "+" | "-" | "*" | "/" | "^"; a: Node; b: Node }
  | { k: "fn"; name: string; a: Node };

const functions: Record<string, (x: number) => number> = {
  sqrt: Math.sqrt,
  abs: Math.abs,
  sin: Math.sin,
  cos: Math.cos,
  tan: Math.tan,
  ln: Math.log,
  log: Math.log10,
  exp: Math.exp,
};

type Token =
  | { t: "num"; v: number }
  | { t: "id"; name: string }
  | { t: "op"; op: string };

function tokenize(text: string): Token[] {
  const tokens: Token[] = [];
  let i = 0;
  const src = text
    .replace(/[×·]/g, "*")
    .replace(/÷/g, "/")
    .replace(/[−–]/g, "-")
    .replace(/\*\*/g, "^");
  while (i < src.length) {
    const c = src[i];
    if (/\s/.test(c)) {
      i += 1;
      continue;
    }
    const number = /^(\d+\.?\d*|\.\d+)/.exec(src.slice(i));
    if (number) {
      tokens.push({ t: "num", v: Number(number[1]) });
      i += number[1].length;
      continue;
    }
    if (c === "π") {
      tokens.push({ t: "id", name: "pi" });
      i += 1;
      continue;
    }
    const word = /^[A-Za-z]+/.exec(src.slice(i));
    if (word) {
      // A run of letters is a function, pi, or variables side by side (xy).
      let rest = word[0];
      while (rest) {
        const name =
          Object.keys(functions).find((fn) => rest.startsWith(fn)) ??
          (rest.startsWith("pi") ? "pi" : rest[0]);
        tokens.push({ t: "id", name });
        rest = rest.slice(name.length);
      }
      i += word[0].length;
      continue;
    }
    if ("+-*/^()".includes(c)) {
      tokens.push({ t: "op", op: c });
      i += 1;
      continue;
    }
    throw new ParseError(`Unexpected "${c}".`);
  }
  return tokens;
}

/** Parse an answer. Throws ParseError on anything outside the grammar. */
export function parse(text: string): Node {
  const stripped = text.trim().replace(/^[A-Za-z]\s*=(?!=)/, "");
  const tokens = tokenize(stripped);
  if (tokens.length === 0) throw new ParseError("Empty answer.");
  let at = 0;
  const peek = () => tokens[at];
  const isOp = (op: string) => {
    const token = peek();
    return token?.t === "op" && token.op === op;
  };
  const startsFactor = () => {
    const token = peek();
    return (
      token !== undefined &&
      (token.t === "num" ||
        token.t === "id" ||
        (token.t === "op" && token.op === "("))
    );
  };

  function expression(): Node {
    let node = term();
    while (isOp("+") || isOp("-")) {
      const op = (tokens[at++] as { op: "+" | "-" }).op;
      node = { k: "bin", op, a: node, b: term() };
    }
    return node;
  }
  function term(): Node {
    let node = unary();
    for (;;) {
      if (isOp("*") || isOp("/")) {
        const op = (tokens[at++] as { op: "*" | "/" }).op;
        node = { k: "bin", op, a: node, b: unary() };
      } else if (startsFactor()) {
        node = { k: "bin", op: "*", a: node, b: power() };
      } else return node;
    }
  }
  function unary(): Node {
    if (isOp("-")) {
      at += 1;
      return { k: "neg", a: unary() };
    }
    if (isOp("+")) {
      at += 1;
      return unary();
    }
    return power();
  }
  function power(): Node {
    const base = atom();
    if (isOp("^")) {
      at += 1;
      return { k: "bin", op: "^", a: base, b: unary() };
    }
    return base;
  }
  function atom(): Node {
    const token = tokens[at++];
    if (!token) throw new ParseError("The answer ends too soon.");
    if (token.t === "num") return { k: "num", v: token.v };
    if (token.t === "id") {
      if (token.name === "pi") return { k: "num", v: Math.PI };
      if (token.name in functions) {
        const a = isOp("(") ? atom() : power();
        return { k: "fn", name: token.name, a };
      }
      return { k: "var", name: token.name };
    }
    if (token.op === "(") {
      const inner = expression();
      if (!isOp(")")) throw new ParseError("A parenthesis is not closed.");
      at += 1;
      return inner;
    }
    throw new ParseError(`Unexpected "${token.op}".`);
  }

  const node = expression();
  if (at < tokens.length) throw new ParseError("Unexpected text at the end.");
  return node;
}

export function variables(node: Node, into = new Set<string>()): Set<string> {
  if (node.k === "var") into.add(node.name);
  else if (node.k === "neg" || node.k === "fn") variables(node.a, into);
  else if (node.k === "bin") {
    variables(node.a, into);
    variables(node.b, into);
  }
  return into;
}

export function evaluate(node: Node, values: Record<string, number>): number {
  switch (node.k) {
    case "num":
      return node.v;
    case "var":
      return values[node.name] ?? Number.NaN;
    case "neg":
      return -evaluate(node.a, values);
    case "fn":
      return functions[node.name](evaluate(node.a, values));
    case "bin": {
      const a = evaluate(node.a, values);
      const b = evaluate(node.b, values);
      switch (node.op) {
        case "+":
          return a + b;
        case "-":
          return a - b;
        case "*":
          return a * b;
        case "/":
          return a / b;
        case "^":
          return a ** b;
      }
    }
  }
}

const close = (a: number, b: number): boolean =>
  Number.isFinite(a) &&
  Number.isFinite(b) &&
  Math.abs(a - b) <= 1e-9 * Math.max(1, Math.abs(a), Math.abs(b));

/** Fixed points, so a grade never changes between two looks. */
const points = [0.5, 1.25, 2, 3.7, -1.3, 0.9, 5.1, 7];

/** Whether `given` equals `key` by value. An unreadable answer is wrong. */
export function sameValue(given: string, key: string): boolean {
  let a: Node;
  let b: Node;
  try {
    a = parse(given);
    b = parse(key);
  } catch (error) {
    if (error instanceof ParseError) return false;
    throw error;
  }
  const names = [...new Set([...variables(a), ...variables(b)])].sort();
  if (names.length === 0) return close(evaluate(a, {}), evaluate(b, {}));
  let compared = 0;
  for (let i = 0; i < points.length; i += 1) {
    const values = Object.fromEntries(
      names.map((name, n) => [name, points[(i + n * 3) % points.length]]),
    );
    const x = evaluate(a, values);
    const y = evaluate(b, values);
    // A point outside one side's domain (sqrt of a negative) is skipped.
    if (!Number.isFinite(x) && !Number.isFinite(y)) continue;
    if (!close(x, y)) return false;
    compared += 1;
  }
  return compared >= 3;
}

/** Why an answer could not be read, for the page to say. */
export function readError(given: string): string | null {
  try {
    parse(given);
    return null;
  } catch (error) {
    if (error instanceof ParseError) return error.message;
    throw error;
  }
}
