"""Proposed local assembly order shared by Blender preview and staged mesh UVs.

Values describe order, not seconds. Runtime timing remains the scene's job.
Input coordinates are Blender -Y forward, +Z up.
"""


def clamp(x):return max(0.,min(1.,x))


def assembly_order(co,group):
    x,back,up=co;forward=-back
    longitudinal=clamp((.70-forward)/2.15)
    if group=='Chassis':return (.04+.16*longitudinal,longitudinal)
    if group.startswith('Engine'):
        p=clamp((.55-forward)/1.95);return (.19+.23*p,p)
    if group=='Cockpit':
        p=clamp((up+.15)/.50);return (.24+.22*p,p)
    if group.startswith('Spar'):
        p=clamp((abs(x)-1.0)/.85);return (.30+.18*p,p)
    if group=='Services':return (.36+.13*longitudinal,longitudinal)
    if group=='Exterior':
        tail=clamp((2.50-forward)/4.14)
        # Broad depth transition: belly resolves first, cockpit/drive tops last.
        top=clamp((up+.08)/.14)
        wing=clamp((abs(x)-1.02)/.50)
        body=(.55+.18*tail)*(1-top)+(.76+.20*tail)*top
        return (body*(1-wing)+(.60+.21*clamp((abs(x)-1)/1))*wing,tail)
    if group=='Systems':return (.965,.5)
    return (.0,longitudinal)
