using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


class Linear : Ease { 
    public static double EaseNone (double t, double b, double c, double d) {
		return c*t/d + b;
	}
	public static double EaseIn (double t, double b, double c, double d) {
		return c*t/d + b;
	}
	public static double EaseOut (double t, double b, double c, double d) {
		return c*t/d + b;
	}
	public static double EaseInOut (double t, double b, double c, double d) {
		return c*t/d + b;
	}
}
