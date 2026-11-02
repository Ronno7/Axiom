"""v13: competing perimeter-inward and control-center-outward arrival times.

Input is source Blender-local coordinates. Returns seconds, before fine surface grain.
The perimeter is an authored envelope approximation, not a geodesic or panel UV map.
"""
import math


def arrival_fronts(co):
    x,back,up=co;forward=-back
    top=max(0,min(1,(up+.1)/.5))
    # Different boundary regions start together with small offsets.
    wing=1.46+.92*max(0,(2-abs(x))/1.15)
    nose=1.54+.92*max(0,(2.5-forward)/1.05)
    rear=1.49+.92*max(0,(forward+1.64)/.95)
    inward=min(wing,nose,rear)+.07*top
    radius=math.sqrt((x/1.05)**2+((forward-.205)/1.65)**2)
    outward=1.40+.92*radius+.07*top
    return inward,outward


def surface_arrival(co):return min(arrival_fronts(co))
