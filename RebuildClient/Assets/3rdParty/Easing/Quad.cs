class Quad : Ease { 
    public static double EaseIn (double t, double b, double c, double d) {
		return c*(t/=d)*t + b;
	}
	public static double EaseOut (double t, double b, double c, double d) {
		return -c *(t/=d)*(t-2) + b;
	}
	public static double EaseInOut (double t, double b, double c, double d) {
		if ((t/=d/2) < 1) return c/2*t*t + b;
		return -c/2 * ((--t)*(t-2) - 1) + b;
	}
}
