class Back : Ease {
    public static double EaseIn(double t, double b, double c, double d)
    {
        double s = 1.70158;
		return c*(t/=d)*t*((s+1)*t - s) + b;
	}
    public static double EaseOut(double t, double b, double c, double d)
    {
        double s = 1.70158;
		return c*((t=t/d-1)*t*((s+1)*t + s) + 1) + b;
	}
    public static double EaseInOut(double t, double b, double c, double d)
    {
        double s = 1.70158;
		if ((t/=d/2) < 1) return c/2*(t*t*(((s*=(1.525))+1)*t - s)) + b;
		return c/2*((t-=2)*t*(((s*=(1.525))+1)*t + s) + 2) + b;
	}


    public static double EaseIn(double t, double b, double c, double d, double s)
    {
		return c*(t/=d)*t*((s+1)*t - s) + b;
	}
    public static double EaseOut(double t, double b, double c, double d, double s)
    {
		return c*((t=t/d-1)*t*((s+1)*t + s) + 1) + b;
	}
    public static double EaseInOut(double t, double b, double c, double d, double s)
    {
		if ((t/=d/2) < 1) return c/2*(t*t*(((s*=(1.525))+1)*t - s)) + b;
		return c/2*((t-=2)*t*(((s*=(1.525))+1)*t + s) + 2) + b;
	}
}
