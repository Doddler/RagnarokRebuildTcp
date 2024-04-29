using System;


public delegate double EaseDelegate(double t, double b, double c, double d);

abstract class Ease {
    protected const double TWO_PI = Math.PI * 2;
    protected const double HALF_PI = Math.PI / 2;
}
