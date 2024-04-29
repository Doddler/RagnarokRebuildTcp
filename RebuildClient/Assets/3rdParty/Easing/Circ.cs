using System;


class Circ : Ease {
    public static double EaseIn(double t, double b, double c, double d)
    {
		return -c * (Math.Sqrt(1 - (t/=d)*t) - 1) + b;
	}
    public static double EaseOut(double t, double b, double c, double d)
    {
		return c * Math.Sqrt(1 - (t=t/d-1)*t) + b;
	}
    public static double EaseInOut(double t, double b, double c, double d)
    {
		if ((t/=d/2) < 1) return -c/2 * (Math.Sqrt(1 - t*t) - 1) + b;
		return c/2 * (Math.Sqrt(1 - (t-=2)*t) + 1) + b;
	}
}
