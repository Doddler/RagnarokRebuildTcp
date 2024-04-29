class Bounce : Ease {
    public static double EaseOut(double t, double b, double c, double d)
    {
		if ((t/=d) < (1/2.75)) {
			return c*(7.5625*t*t) + b;
		} else if (t < (2/2.75)) {
			return c*(7.5625*(t-=(1.5/2.75))*t + .75) + b;
		} else if (t < (2.5/2.75)) {
			return c*(7.5625*(t-=(2.25/2.75))*t + .9375) + b;
		} else {
			return c*(7.5625*(t-=(2.625/2.75))*t + .984375) + b;
		}
	}
    public static double EaseIn(double t, double b, double c, double d)
    {
		return c - EaseOut(d-t, 0, c, d) + b;
	}
    public static double EaseInOut(double t, double b, double c, double d)
    {
		if (t < d/2) return EaseIn (t*2, 0, c, d) * .5 + b;
		else return EaseOut (t*2-d, 0, c, d) * .5 + c*.5 + b;
	}
}
