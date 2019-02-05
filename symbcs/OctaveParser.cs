﻿using System.Collections;
using System.IO;
using System.Text;

internal class OctaveParser : Parser
{
	internal Lambda CRV = new CreateVector();
	internal Lambda REF = new REFM();
	internal int IN_PARENT = 1;
	internal int IN_BRACK = 2;
	internal int IN_BLOCK = 4;
	internal Rule[] rules;
	internal string[][] rules_in = new string[][]
	{
		new string[] {"function  y = f  X end", "X f y 3 @FUNC"},
		new string[] {"if u X else Y end", "Y X u 3 @BRANCH"},
		new string[] {"if u X end", "X u 2 @BRANCH"},
		new string[] {"for u X end", "X u 2 @FOR"},
		new string[] {"while u X end", "X u 2 @WHILE"}
	};
	internal string[] commands = new string[] {"format", "hold", "syms", "clear", "addpath"};
	public OctaveParser(Environment env) : base(env)
	{
		env.addPath(".");
		env.addPath("m");
		env.globals.Add("pi", Zahl.PI);
        env.globals.Add( "i", Zahl.IONE );
        env.globals.Add( "j", Zahl.IONE );
        env.globals.Add( "eps", new Unexakt( 2.220446049250313E-16 ) );
        env.globals.Add( "ratepsilon", new Unexakt( 2.0e-8 ) );
        env.globals.Add( "algepsilon", new Unexakt( 1.0e-8 ) );
        env.globals.Add( "rombergit", new Unexakt( 11 ) );
	    env.globals.Add( "rombergtol", new Unexakt( 1.0e-4 ) );
		pst = new ParserState(null, 0);
        Operator.OPS = new Operator[]
        {
            new Operator( "POW", ".**", 1, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "PPR", "++", 1, Constants_Fields.RIGHT_LEFT, Constants_Fields.UNARY | Constants_Fields.LVALUE ), 
            new Operator( "MMR", "--", 1, Constants_Fields.RIGHT_LEFT, Constants_Fields.UNARY | Constants_Fields.LVALUE ), 
            new Operator( "PPL", "++", 1, Constants_Fields.LEFT_RIGHT, Constants_Fields.UNARY | Constants_Fields.LVALUE ), 
            new Operator( "MML", "--", 1, Constants_Fields.LEFT_RIGHT, Constants_Fields.UNARY | Constants_Fields.LVALUE ), 
            new Operator( "ADE", "+=", 10, Constants_Fields.RIGHT_LEFT, Constants_Fields.BINARY | Constants_Fields.LVALUE ), 
            new Operator( "SUE", "-=", 10, Constants_Fields.RIGHT_LEFT, Constants_Fields.BINARY | Constants_Fields.LVALUE ), 
            new Operator( "MUE", "*=", 10, Constants_Fields.RIGHT_LEFT, Constants_Fields.BINARY | Constants_Fields.LVALUE ), 
            new Operator( "DIE", "/=", 10, Constants_Fields.RIGHT_LEFT, Constants_Fields.BINARY | Constants_Fields.LVALUE ), 
            new Operator( "MPW", "**", 1, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "MUL", ".*", 3, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "DIV", "./", 3, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "POW", ".^", 1, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "EQU", "==", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "NEQ", "~=", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "GEQ", ">=", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "LEQ", "<=", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "TRN", ".'", 1, Constants_Fields.RIGHT_LEFT, Constants_Fields.UNARY ), 
            new Operator( "GRE", ">", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "LES", "<", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "OR", "|", 9, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "NOT", "~", 8, Constants_Fields.LEFT_RIGHT, Constants_Fields.UNARY ), 
            new Operator( "AND", "&", 7, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "GRE", ">", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "GRE", ">", 6, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "ASS", "=", 10,Constants_Fields. RIGHT_LEFT, Constants_Fields.BINARY | Constants_Fields.LVALUE ), 
            new Operator( "CR1", ":", 5, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY | Constants_Fields.TERNARY ), 
            new Operator( "ADD", "+", 4, Constants_Fields.LEFT_RIGHT, Constants_Fields.UNARY | Constants_Fields.BINARY ), 
            new Operator( "SUB", "-", 4, Constants_Fields.LEFT_RIGHT, Constants_Fields.UNARY | Constants_Fields.BINARY ), 
            new Operator( "MMU", "*", 3, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "MDR", "/", 3, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "MDL", "\\", 3, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "MPW", "^", 1, Constants_Fields.LEFT_RIGHT, Constants_Fields.BINARY ), 
            new Operator( "ADJ", "'", 1, Constants_Fields.RIGHT_LEFT, Constants_Fields.UNARY )
        };
		for (int i = 0; i < Operator.OPS.Length; i++)
		{
			nonsymbols.Add(Operator.OPS[i].symbol);
		}
		for (int i = 0; i < listsep.Length; i++)
		{
			nonsymbols.Add(listsep[i]);
		}
		for (int i = 0; i < commands.Length; i++)
		{
			nonsymbols.Add(commands[i]);
		}
		for (int i = 0; i < keywords.Length; i++)
		{
			nonsymbols.Add(keywords[i]);
		}
		try
		{
			rules = compile_rules(rules_in);
		}
		catch (ParseException)
		{
		}
		Lambda.pr = this;
	}
	public override string prompt()
	{
		return ">> ";
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: public List compile(InputStream is, PrintStream ps) throws ParseException, IOException
	public override List compile( Stream istream, PrintStream ps)
	{
		string s , sp = null;
		reset();
        while ( ( s = readLine( istream ) ) != null )
		{
			sp = s;
			translate(s);
			if (ready())
			{
				break;
			}
			else
			{
				if (ps != null)
				{
					ps.print("> ");
				}
			}
		}
		if (sp == null)
		{
			return null;
		}
		if (s == null && pst.inList == IN_BLOCK)
		{
			List v = pst.tokens;
			pst = (ParserState)pst.sub;
			pst.tokens.Add(v);
		}
		return get();
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: public List compile(String s) throws ParseException
	public override List compile(string s)
	{
		reset();
		translate(s);
		return get();
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List get() throws ParseException
	internal override List get()
	{
		List r = pst.tokens;
		List pgm = compile_statement(r);
		if (pgm != null)
		{
			return pgm;
		}
		throw new ParseException("Compilation failed.");
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: void translate(String s) throws ParseException
	internal override void translate(string s)
	{
		if (s == null)
		{
			return;
		}
		StringBuilder sb = new StringBuilder(s);
		object t;
		while ((t = nextToken(sb)) != null)
		{
			pst.tokens.Add(t);
			pst.prev = t;
		}
	}
	internal static string FUNCTION = "function", FOR = "for", WHILE = "while", IF = "if", ELSE = "else", END = "end", BREAK = "break", RETURN = "return", CONTINUE = "continue", EXIT = "exit";
	private string[] keywords = new string[] {FUNCTION, FOR, WHILE, IF, ELSE, END, BREAK, RETURN, CONTINUE, EXIT};
	private string sepright = ")]*/^!,;:=.<>'\\";
	private string sepleft = "*/^!,;:=.<>'\\+-";
	internal virtual bool refq(object expr)
	{
		return expr is string && ((string)expr).Length > 0 && ((string)expr)[0] == '@';
	}
	internal override bool commandq(object x)
	{
		return oneof(x, commands);
	}
	internal virtual bool operatorq(object expr)
	{
		return Operator.get(expr) != null;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: public Object nextToken(StringBuffer s) throws ParseException
	public virtual object nextToken(StringBuilder s)
	{
		if (pst.inList == IN_BRACK && pst.prev != null && !oneof(pst.prev, sepleft))
		{
			int k = 0;
			for (; k < s.Length && whitespace(s[k]); k++)
			{
				;
			}
			if (k == s.Length)
			{
				s.Remove(0, k);
				return ";";
			}
			else if (k > 0)
			{
				char c = s[k];
				if (c == '+' || c == '-')
				{
					if (s.Length > k + 1 && !whitespace(s[k + 1]))
					{
						s.Remove(0, k);
						return ",";
					}
				}
				else if (!oneof(s[k],sepright))
				{
					s.Remove(0, k);
					return ",";
				}
			}
		}
		if (pst.inList == IN_BLOCK && pst.prev != null && !oneof(pst.prev, listsep))
		{
			int k = 0;
			for (; k < s.Length && whitespace(s[k]); k++)
			{
				;
			}
			if (k == s.Length)
			{
				s.Remove(0, k);
				return ",";
			}
		}
		skipWhitespace(s);
		if (s.Length < 1)
		{
			return null;
		}
		char c0 = s[0];
		switch (c0)
		{
			case '"':
				return ' ' + cutstring(s,'"','"');
			case '(':
				if (symbolq(pst.prev))
				{
					pst.prev = "@" + pst.prev;
					pst.tokens.Remove(pst.tokens.Count - 1);
					pst.tokens.Add(pst.prev);
				}
				pst = new ParserState(pst, IN_PARENT);
				return nextToken(s.Remove(0, 1));
			case ')':
				if (pst.inList != IN_PARENT)
				{
					throw new ParseException("Wrong parenthesis.");
				}
				List t = pst.tokens;
				pst = (ParserState)pst.sub;
				s.Remove(0, 1);
				return t;
			case '[':
				pst = new ParserState(pst, IN_BRACK);
				return nextToken(s.Remove(0, 1));
			case ']':
				if (pst.inList != IN_BRACK)
				{
					throw new ParseException("Wrong brackets.");
				}
				t = pst.tokens;
				while (t.Count > 0 && ";".Equals(t[t.Count - 1]))
				{
					t.Remove(t.Count - 1);
				}
				t.Insert(0, "[");
				pst = (ParserState)pst.sub;
				s.Remove(0, 1);
				return t;
			case '%':
		case '#':
			s.Remove(0, s.Length);
			return null;
		case '\'':
			if (pst.prev == null || stringopq(pst.prev))
			{
				return ' ' + cutstring(s,'\'','\'');
			}
			else
			{
				return readString(s);
			}
			case ';':
		case ',':
			s.Remove(0, 1);
			return "" + c0;
		case '0':
	case '1':
case '2':
case '3':
case '4':
case '5':
case '6':
case '7':
case '8':
case '9':
	return readNumber(s);
case '.':
	if (s.Length > 1 && number(s[1]))
	{
		return readNumber(s);
	}
	else
	{
		return readString(s);
	}
	default :
		return readString(s);
		}
	}
	internal override bool ready()
	{
		return pst.sub == null;
	}
	private string separator = "()[]\n\t\r +-*/^!,;:=.<>'\\&|";
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: Object readString(StringBuffer s)throws ParseException
	internal virtual object readString(StringBuilder s)
	{
		int len = s.Length > 2?3:s.Length;
		//char[] substring = new char[len];
		//s.getChars(0,len,substring,0);
		//string st = new string(substring);
	    string st = s.ToString().Substring( 0, len );

		Operator op = Operator.get(st);
		if (op != null)
		{
			s.Remove(0, op.symbol.Length);
			return op.symbol;
		}
		int k = 1;
		while (k < s.Length && !oneof(s[k], separator))
		{
			k++;
		}
		//substring = new char[k];
		//s.getChars(0,k,substring,0);
		//string t = new string(substring);

	    string t = s.ToString().Substring( 0, k );

		s.Remove(0, k);
		if (t.Equals(IF) || t.Equals(FOR) || t.Equals(WHILE) || t.Equals(FUNCTION))
		{
			if (pst.inList == IN_PARENT || pst.inList == IN_BRACK)
			{
				throw new ParseException("Block starts within list.");
			}
			pst.tokens.Add(t);
			pst = new ParserState(pst, IN_BLOCK);
			return nextToken(s);
		}
		if (t.Equals(ELSE))
		{
			if (pst.inList != IN_BLOCK)
			{
				throw new ParseException("Orphaned else.");
			}
			List v = pst.tokens;
			((ParserState)pst.sub).tokens.Add(v);
			pst = new ParserState(pst.sub, IN_BLOCK);
			return ELSE;
		}
		if (t.Equals(END))
		{
			if (pst.inList != IN_BLOCK)
			{
				throw new ParseException("Orphaned end.");
			}
			List v = pst.tokens;
			pst = (ParserState)pst.sub;
			return v;
		}
		return t;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_unary(Operator op, List expr)throws ParseException
	internal virtual List compile_unary(Operator op, List expr)
	{
		List arg_in = (op.left_right() ? expr.subList(1, expr.Count) : expr.subList(0, expr.Count - 1));
		List arg = (op.lvalue() ? compile_lval(arg_in) : compile_expr(arg_in));
		if (arg == null)
		{
			return null;
		}
		arg.Add(ONE);
		arg.Add(op.Lambda);
		return arg;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_ternary(Operator op, List expr, int k)throws ParseException
	internal virtual List compile_ternary(Operator op, List expr, int k)
	{
		int n = expr.Count;
		for (int k0 = k - 2; k0 > 0; k0--)
		{
			if (op.symbol.Equals(expr[k0]))
			{
				List left_in = expr.subList(0, k0);
				List left = compile_expr(left_in);
				if (left == null)
				{
					continue;
				}
				List mid_in = expr.subList(k0 + 1, k);
				List mid = compile_expr(mid_in);
				if (mid == null)
				{
					continue;
				}
				List right_in = expr.subList(k + 1, expr.Count);
				List right = compile_expr(right_in);
				if (right == null)
				{
					continue;
				}
				left.AddRange(mid);
				left.AddRange(right);
				left.Add(THREE);
				left.Add(op.Lambda);
				return left;
			}
		}
		return null;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_binary(Operator op, List expr, int k)throws ParseException
	internal virtual List compile_binary(Operator op, List expr, int k)
	{
		List left_in = expr.subList(0, k);
		List left = (op.lvalue() ? compile_lval(left_in) : compile_expr(left_in));
		if (left == null)
		{
			return null;
		}
		;
		List right_in = expr.subList(k + 1, expr.Count);
		List right = compile_expr(right_in);
		if (right == null)
		{
			return null;
		}
		int? nargs = TWO;
		if (op.lvalue())
		{
			object left_narg = left[0];
			if (left_narg is int?)
			{
				nargs = (int?)left_narg;
				right.Insert(right.Count - 1, "#" + nargs);
				left.RemoveAt(0);
			}
			else
			{
				nargs = ONE;
			}
		}
		left.AddRange(right);
		left.Add(nargs);
		left.Add(op.Lambda);
		return left;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List translate_op(List expr) throws ParseException
	internal virtual List translate_op(List expr)
	{
		List s;
		int n = expr.Count;
		for (int pred = 10; pred >= 0; pred--)
		{
			for (int i = 0; i < n; i++)
			{
				int k = i;
				if (pred != 6)
				{
					k = n - i - 1;
				}
                Operator op = Operator.get( expr[ k ], k == 0 ? Constants_Fields.START : ( k == n - 1 ? Constants_Fields.END : Constants_Fields.MID ) );
				if (op == null || op.precedence != pred)
				{
					continue;
				}
				if (op.unary() && ((k == 0 && op.left_right()) || (k == n - 1 && !op.left_right())))
				{
					s = compile_unary(op, expr);
					if (s != null)
					{
						return s;
					}
					else
					{
						continue;
					}
				}
				if (k > 2 && k < n - 1 && op.ternary())
				{
					s = compile_ternary(op, expr, k);
					if (s != null)
					{
						return s;
					}
				}
				if (k > 0 && k < n - 1 && op.binary())
				{
					s = compile_binary(op, expr, k);
					if (s != null)
					{
						return s;
					}
				}
			}
		}
		return null;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_vektor(List expr) throws ParseException
	internal virtual List compile_vektor(List expr)
	{
		if (expr == null || expr.Count == 0 || !"[".Equals(expr[0]))
		{
			return null;
		}
		expr = expr.subList(1, expr.Count);
		List r = Comp.vec2list(new ArrayList());
		int i = 0, ip = 0, nrow = 1;
		while ((i = nextIndexOf(";",ip,expr)) != -1)
		{
			List x = expr.subList(ip, i);
			List xs = compile_list(x);
			if (xs == null)
			{
				return null;
			}
			xs.AddRange(r);
			r = xs;
			nrow++;
			ip = i + 1;
		}
		List x1 = expr.subList(ip, expr.Count);
		List xs1 = compile_list(x1);
		if (xs1 == null)
		{
			return null;
		}
		xs1.AddRange(r);
		r = xs1;
		r.Add(new int?(nrow));
		r.Add(CRV);
		return r;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_list(List expr) throws ParseException
	internal override List compile_list(List expr)
	{
		if (expr == null)
		{
			return null;
		}
		List r = Comp.vec2list(new ArrayList());
		if (expr.Count == 0)
		{
			r.Add(new int?(0));
			return r;
		}
		int i , ip = 0, n = 1;
		while ((i = nextIndexOf(",",ip,expr)) != -1)
		{
			List x = expr.subList(ip, i);
			List xs = compile_expr(x);
			if (xs == null)
			{
				return null;
			}
			xs.AddRange(r);
			r = xs;
			n++;
			ip = i + 1;
		}
		List x1 = expr.subList(ip, expr.Count);
		List xs1 = compile_expr(x1);
		if (xs1 == null)
		{
			return null;
		}
		xs1.AddRange(r);
		r = xs1;
		r.Add(new int?(n));
		return r;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_lval(List expr) throws ParseException
	internal override List compile_lval(List expr)
	{
		if (expr == null || expr.Count == 0)
		{
			return null;
		}
		List r = compile_lval1(expr);
		if (r != null)
		{
			return r;
		}
		if (expr.Count == 1)
		{
			if (expr[0] is List)
			{
				return compile_lval((List)expr[0]);
			}
			else
			{
				return null;
			}
		}
		if (!"[".Equals(expr[0]))
		{
			return null;
		}
		expr = expr.subList(1, expr.Count);
		r = Comp.vec2list(new ArrayList());
		int i , n = 1;
		while ((i = expr.IndexOf(",")) != -1)
		{
			List x = expr.subList(0, i);
			List xs1 = compile_lval1(x);
			if (xs1 == null)
			{
				return null;
			}
			xs1.AddRange(r);
			r = xs1;
			expr = expr.subList(i + 1, expr.Count);
			n++;
		}
		List xs = compile_lval1(expr);
		if (xs == null)
		{
			return null;
		}
		xs.AddRange(r);
		r = xs;
		r.Insert(0, new int?(n));
		return r;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_lval1(List expr) throws ParseException
	internal virtual List compile_lval1(List expr)
	{
		if (expr == null)
		{
			return null;
		}
		switch (expr.Count)
		{
			case 1:
				object x = expr[0];
				if (x is List)
				{
					return compile_lval1((List)x);
				}
				if (symbolq(x) && !refq(x))
				{
					List s = Comp.vec2list(new ArrayList());
					s.Add("$" + x);
					return s;
				}
				return null;
			case 2:
				x = expr[0];
				if (!symbolq(x) || !refq(x) || !(expr[1] is List))
				{
					return null;
				}
				List @ref = compile_index((List)expr[1]);
				if (@ref == null)
				{
					return null;
				}
				@ref.Add("$" + ((string)x).Substring(1));
				return @ref;
			default:
				return null;
		}
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_index(List expr) throws ParseException
	internal virtual List compile_index(List expr)
	{
		if (expr == null || expr.Count == 0)
		{
			return null;
		}
		if (expr.Count == 1 && ":".Equals(expr[0]))
		{
			List s = Comp.vec2list(new ArrayList());
			s.Add(":");
			s.Add(ONE);
			return s;
		}
		List r = compile_expr(expr);
		if (r != null)
		{
			r.Add(ONE);
			return r;
		}
		int c = expr.IndexOf(",");
		if (c == -1)
		{
			return null;
		}
		List left_in = expr.subList(0, c);
		List right_in = expr.subList(c + 1, expr.Count);
		if (left_in != null && left_in.Count == 1 && ":".Equals(left_in[0]))
		{
			if (right_in != null && right_in.Count == 1 && ":".Equals(right_in[0]))
			{
				List s = Comp.vec2list(new ArrayList());
				s.Add(":");
				s.Add(":");
				s.Add(TWO);
				return s;
			}
			else
			{
				List right = compile_expr(right_in);
				if (right == null)
				{
					return null;
				}
				right.Add(":");
				right.Add(TWO);
				return right;
			}
		}
		else
		{
			List left = compile_expr(left_in);
			if (left == null)
			{
				return null;
			}
			if (right_in != null && right_in.Count == 1 && ":".Equals(right_in[0]))
			{
				left.Insert(0, ":");
				left.Add(TWO);
				return left;
			}
			else
			{
				List right = compile_expr(right_in);
				if (right == null)
				{
					return null;
				}
				right.AddRange(left);
				right.Add(TWO);
				return right;
			}
		}
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_statement(List expr_in) throws ParseException
	internal override List compile_statement(List expr_in)
	{
		if (expr_in == null)
		{
			return null;
		}
		if (expr_in.Count == 0)
		{
			return Comp.vec2list(new ArrayList());
		}
		List expr = Comp.clonelist(expr_in);
		object first = expr[0];
		for (int i = 0; i < rules.Length; i++)
		{
			Rule r = rules[i];
			if (r.rule_in[0].Equals(first) && expr.Count >= r.rule_in.Count)
			{
				Compiler c = new Compiler(r.rule_in, r.rule_out, this);
				List expr_sub = expr.subList(0, r.rule_in.Count);
				List s = c.compile(expr_sub);
				if (s != null)
				{
					Comp.clear(expr, 0, expr_sub.Count);
					if (expr.Count == 0)
					{
						return s;
					}
					List t = compile_statement(expr);
					if (t == null)
					{
						return null;
					}
					s.AddRange(t);
					return s;
				}
			}
		}
		if (commandq(first))
		{
			List expr_sub = expr;
			int indx = expr.IndexOf(";");
			if (indx > 0)
			{
				expr_sub = expr.subList(0, indx + 1);
			}
			List s = compile_command(expr_sub);
			if (s != null)
			{
				if (indx > 0)
				{
					s.Add("#;");
					Comp.clear(expr, 0, indx + 1);
					List t = compile_statement(expr);
					if (t == null)
					{
						return null;
					}
					s.AddRange(t);
				}
				return s;
			}
			return null;
		}
		string lend = null;
		int ic = expr.IndexOf(",");
		int indx1 = expr.IndexOf(";");
		if (ic >= 0 && (ic < indx1 || indx1 == -1))
		{
			lend = "#,";
		}
		else if (indx1 >= 0 && (indx1 < ic || ic == -1))
		{
			lend = "#;";
			ic = indx1;
		}
		if (ic == 0)
		{
			Comp.clear(expr,0,1);
			return compile_statement(expr);
		}
		if (lend != null)
		{
			List expr_sub = expr.subList(0, ic);
			List s = compile_expr(expr_sub);
			if (s != null)
			{
				s.Add(lend);
				Comp.clear(expr, 0, ic + 1);
				if (expr.Count == 0)
				{
					return s;
				}
				List t = compile_statement(expr);
				if (t == null)
				{
					return null;
				}
				s.AddRange(t);
				return s;
			}
		}
		else
		{
			return compile_expr(expr);
		}
		return null;
	}
	internal virtual string compile_keyword(object x)
	{
		if (x.Equals(BREAK))
		{
			return "#brk";
		}
		else if (x.Equals(CONTINUE))
		{
			return "#cont";
		}
		else if (x.Equals(EXIT))
		{
			return "#exit";
		}
		else if (x.Equals(RETURN))
		{
			return "#ret";
		}
		return null;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_func(List expr) throws ParseException
	internal override List compile_func(List expr)
	{
		if (expr.Count == 2)
		{
			object op = expr[0];
			object ref_in = expr[1];
			if (symbolq(op) && refq(op) && ref_in is List)
			{
				List @ref = compile_list((List)ref_in);
				if (@ref != null)
				{
					@ref.Add(op);
					return @ref;
				}
			}
		}
		return null;
	}
//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: List compile_expr(List expr) throws ParseException
	internal override List compile_expr(List expr)
	{
		if (expr == null || expr.Count == 0)
		{
			return null;
		}
		if (expr.Count == 1)
		{
			object x = expr[0];
			if (x is Algebraic)
			{
				List s = Comp.vec2list(new ArrayList());
				s.Add(x);
				return s;
			}
			if (x is string)
			{
				object y = compile_keyword(x);
				if (y != null)
				{
					List s = Comp.vec2list(new ArrayList());
					s.Add(y);
					return s;
				}
				if (stringq(x))
				{
					List s = Comp.vec2list(new ArrayList());
					s.Add(x);
					return s;
				}
				if (symbolq(x))
				{
					if (refq(x))
					{
						x = "$" + ((string)x).Substring(1);
					}
					List s = Comp.vec2list(new ArrayList());
					s.Add(x);
					return s;
				}
				return null;
			}
			if (x is List)
			{
				List xs = compile_vektor((List)x);
				if (xs != null)
				{
					return xs;
				}
				return compile_expr((List)x);
			}
		}
		List res = compile_func(expr);
		if (res != null)
		{
			return res;
		}
		res = translate_op(expr);
		if (res != null)
		{
			return res;
		}
		object ref_in = expr[expr.Count - 1];
		if (!(ref_in is List))
		{
			return null;
		}
		List @ref = compile_index((List)ref_in);
		if (@ref == null)
		{
			return null;
		}
		List left_in = expr.subList(0,expr.Count - 1);
		if (left_in.Count == 1 && symbolq(left_in[0]) && refq(left_in[0]))
		{
			@ref.AddRange(left_in);
			return @ref;
		}
		List left = compile_expr(left_in);
		if (left != null)
		{
			@ref.AddRange(left);
			@ref.Add(TWO);
			@ref.Add(REF);
			return @ref;
		}
		return null;
	}
}